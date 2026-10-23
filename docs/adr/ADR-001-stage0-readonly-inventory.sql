-- ADR-001 stage 0: read-only inventory of Enrollment -> OrderItem candidates.
-- Target: PostgreSQL on ffa_test or a local restored copy.
-- Safety: this script is read-only; it creates no tables, functions, or temp objects.
--
-- Output sections:
-- 1. Detailed per-enrollment report with A/B/C/D/E classification.
-- 2. Category counts.
-- 3. Manual review list for C/D/E.
-- 4. Group.PackageId integrity checks.
--
-- Category precedence:
-- D -> E -> A -> B -> C
-- D: no order / broken references / zero order items / broken group reference
-- E: no cohort, or cohort has no product
-- A: order has exactly one order item
-- B: order has multiple order items, but exactly one strict match by cohort.product_id
--    and, when group exists, by group.package_id
-- C: ambiguous candidates: zero or multiple strict matches among multiple items

WITH base AS (
    SELECT
        e."Id" AS enrollment_id,
        e."OrderId" AS order_id,
        e."CohortId" AS cohort_id,
        e."GroupId" AS group_id,
        o."Id" AS order_exists_id,
        c."Id" AS cohort_exists_id,
        c."ProductId" AS cohort_product_id,
        g."Id" AS group_exists_id,
        g."CohortId" AS group_cohort_id,
        g."PackageId" AS group_package_id,
        gp.id AS group_package_exists_id,
        gp.product_id AS group_package_product_id
    FROM enrollments e
    LEFT JOIN orders o
        ON o."Id" = e."OrderId"
    LEFT JOIN cohorts c
        ON c."Id" = e."CohortId"
    LEFT JOIN groups g
        ON g."Id" = e."GroupId"
    LEFT JOIN packages gp
        ON gp.id = g."PackageId"
),
candidates AS (
    SELECT
        b.enrollment_id,
        oi."Id" AS candidate_order_item_id,
        oi.product_id AS candidate_product_id,
        oi.package_id AS candidate_package_id,
        CASE
            WHEN b.cohort_product_id IS NOT NULL
             AND oi.product_id = b.cohort_product_id
             AND (
                    b.group_id IS NULL
                 OR b.group_package_id IS NULL
                 OR oi.package_id = b.group_package_id
             )
            THEN 1 ELSE 0
        END AS strict_match,
        CASE
            WHEN b.cohort_product_id IS NOT NULL
             AND oi.product_id = b.cohort_product_id
            THEN 1 ELSE 0
        END AS product_match
    FROM base b
    LEFT JOIN order_items oi
        ON oi.order_id = b.order_id
),
agg AS (
    SELECT
        b.enrollment_id,
        b.order_id,
        b.cohort_id,
        b.cohort_product_id,
        b.group_id,
        b.group_package_id,
        b.order_exists_id,
        b.cohort_exists_id,
        b.group_exists_id,
        b.group_cohort_id,
        b.group_package_exists_id,
        b.group_package_product_id,
        COUNT(c.candidate_order_item_id) AS candidate_count,
        COALESCE(SUM(c.strict_match), 0) AS strict_match_count,
        COALESCE(SUM(c.product_match), 0) AS product_match_count,
        ARRAY_REMOVE(ARRAY_AGG(c.candidate_order_item_id ORDER BY c.candidate_order_item_id), NULL) AS candidate_order_item_ids,
        COALESCE(
            JSONB_AGG(
                JSONB_BUILD_OBJECT(
                    'orderItemId', c.candidate_order_item_id,
                    'productId', c.candidate_product_id,
                    'packageId', c.candidate_package_id,
                    'strictMatch', c.strict_match = 1
                )
                ORDER BY c.candidate_order_item_id
            ) FILTER (WHERE c.candidate_order_item_id IS NOT NULL),
            '[]'::jsonb
        ) AS candidate_items
    FROM base b
    LEFT JOIN candidates c
        ON c.enrollment_id = b.enrollment_id
    GROUP BY
        b.enrollment_id,
        b.order_id,
        b.cohort_id,
        b.cohort_product_id,
        b.group_id,
        b.group_package_id,
        b.order_exists_id,
        b.cohort_exists_id,
        b.group_exists_id,
        b.group_cohort_id,
        b.group_package_exists_id,
        b.group_package_product_id
),
classified AS (
    SELECT
        a.*,
        CASE
            WHEN a.order_id IS NULL
              OR a.order_exists_id IS NULL
              OR (a.group_id IS NOT NULL AND a.group_exists_id IS NULL)
              OR a.candidate_count = 0
            THEN 'D'
            WHEN a.cohort_id IS NULL
              OR a.cohort_exists_id IS NULL
              OR a.cohort_product_id IS NULL
            THEN 'E'
            WHEN a.candidate_count = 1
            THEN 'A'
            WHEN a.candidate_count > 1
             AND a.strict_match_count = 1
            THEN 'B'
            ELSE 'C'
        END AS classification,
        CASE
            WHEN a.order_id IS NULL
              OR a.order_exists_id IS NULL
              OR (a.group_id IS NOT NULL AND a.group_exists_id IS NULL)
              OR a.candidate_count = 0
            THEN 'low'
            WHEN a.candidate_count = 1
            THEN 'high'
            WHEN a.candidate_count > 1
             AND a.strict_match_count = 1
            THEN 'medium'
            ELSE 'low'
        END AS confidence,
        CASE
            WHEN a.order_id IS NULL THEN 'Enrollment.OrderId is null.'
            WHEN a.order_exists_id IS NULL THEN 'Enrollment references a missing order.'
            WHEN a.group_id IS NOT NULL AND a.group_exists_id IS NULL THEN 'Enrollment references a missing group.'
            WHEN a.candidate_count = 0 THEN 'Order has no order_items, so no candidate can explain the enrollment.'
            WHEN a.cohort_id IS NULL THEN 'Enrollment has no cohort; product cannot be derived.'
            WHEN a.cohort_exists_id IS NULL THEN 'Enrollment references a missing cohort; product cannot be derived.'
            WHEN a.cohort_product_id IS NULL THEN 'Cohort exists but ProductId is null; product cannot be derived.'
            WHEN a.candidate_count = 1 THEN 'Order has exactly one OrderItem.'
            WHEN a.candidate_count > 1 AND a.strict_match_count = 1 AND a.group_id IS NULL
                THEN 'Multiple order items, but exactly one matches Cohort.ProductId.'
            WHEN a.candidate_count > 1 AND a.strict_match_count = 1 AND a.group_id IS NOT NULL
                THEN 'Multiple order items, but exactly one matches Cohort.ProductId and Group.PackageId.'
            WHEN a.candidate_count > 1 AND a.strict_match_count = 0 AND a.product_match_count = 1
                THEN 'Exactly one candidate matches cohort product, but it does not match Group.PackageId.'
            WHEN a.candidate_count > 1 AND a.strict_match_count = 0 AND a.product_match_count = 0
                THEN 'No order item matches Cohort.ProductId.'
            WHEN a.candidate_count > 1 AND a.strict_match_count > 1
                THEN 'Several order items satisfy the product/package rule; mapping is ambiguous.'
            ELSE 'Ambiguous enrollment-to-order-item mapping.'
        END AS reason
    FROM agg a
)
SELECT
    enrollment_id AS "enrollmentId",
    order_id AS "currentOrderId",
    cohort_id AS "cohortId",
    cohort_product_id AS "cohortProductId",
    group_id AS "groupId",
    group_package_id AS "groupPackageId",
    candidate_order_item_ids AS "candidateOrderItemIds",
    candidate_items AS "candidateItems",
    classification,
    confidence,
    reason
FROM classified
ORDER BY classification, enrollment_id;

WITH base AS (
    SELECT
        e."Id" AS enrollment_id,
        e."OrderId" AS order_id,
        e."CohortId" AS cohort_id,
        e."GroupId" AS group_id,
        o."Id" AS order_exists_id,
        c."Id" AS cohort_exists_id,
        c."ProductId" AS cohort_product_id,
        g."Id" AS group_exists_id,
        g."CohortId" AS group_cohort_id,
        g."PackageId" AS group_package_id,
        gp.id AS group_package_exists_id,
        gp.product_id AS group_package_product_id
    FROM enrollments e
    LEFT JOIN orders o ON o."Id" = e."OrderId"
    LEFT JOIN cohorts c ON c."Id" = e."CohortId"
    LEFT JOIN groups g ON g."Id" = e."GroupId"
    LEFT JOIN packages gp ON gp.id = g."PackageId"
),
candidates AS (
    SELECT
        b.enrollment_id,
        oi."Id" AS candidate_order_item_id,
        oi.product_id AS candidate_product_id,
        oi.package_id AS candidate_package_id,
        CASE
            WHEN b.cohort_product_id IS NOT NULL
             AND oi.product_id = b.cohort_product_id
             AND (
                    b.group_id IS NULL
                 OR b.group_package_id IS NULL
                 OR oi.package_id = b.group_package_id
             )
            THEN 1 ELSE 0
        END AS strict_match,
        CASE
            WHEN b.cohort_product_id IS NOT NULL
             AND oi.product_id = b.cohort_product_id
            THEN 1 ELSE 0
        END AS product_match
    FROM base b
    LEFT JOIN order_items oi ON oi.order_id = b.order_id
),
agg AS (
    SELECT
        b.enrollment_id,
        b.order_id,
        b.cohort_id,
        b.cohort_product_id,
        b.group_id,
        b.group_package_id,
        b.order_exists_id,
        b.cohort_exists_id,
        b.group_exists_id,
        COUNT(c.candidate_order_item_id) AS candidate_count,
        COALESCE(SUM(c.strict_match), 0) AS strict_match_count
    FROM base b
    LEFT JOIN candidates c ON c.enrollment_id = b.enrollment_id
    GROUP BY
        b.enrollment_id,
        b.order_id,
        b.cohort_id,
        b.cohort_product_id,
        b.group_id,
        b.group_package_id,
        b.order_exists_id,
        b.cohort_exists_id,
        b.group_exists_id
),
classified AS (
    SELECT
        enrollment_id,
        CASE
            WHEN order_id IS NULL
              OR order_exists_id IS NULL
              OR (group_id IS NOT NULL AND group_exists_id IS NULL)
              OR candidate_count = 0
            THEN 'D'
            WHEN cohort_id IS NULL
              OR cohort_exists_id IS NULL
              OR cohort_product_id IS NULL
            THEN 'E'
            WHEN candidate_count = 1
            THEN 'A'
            WHEN candidate_count > 1
             AND strict_match_count = 1
            THEN 'B'
            ELSE 'C'
        END AS classification
    FROM agg
)
SELECT
    classification,
    COUNT(*) AS enrollment_count
FROM classified
GROUP BY classification
ORDER BY classification;

WITH base AS (
    SELECT
        e."Id" AS enrollment_id,
        e."OrderId" AS order_id,
        e."CohortId" AS cohort_id,
        e."GroupId" AS group_id,
        o."Id" AS order_exists_id,
        c."Id" AS cohort_exists_id,
        c."ProductId" AS cohort_product_id,
        g."Id" AS group_exists_id,
        g."PackageId" AS group_package_id
    FROM enrollments e
    LEFT JOIN orders o ON o."Id" = e."OrderId"
    LEFT JOIN cohorts c ON c."Id" = e."CohortId"
    LEFT JOIN groups g ON g."Id" = e."GroupId"
),
candidates AS (
    SELECT
        b.enrollment_id,
        oi."Id" AS candidate_order_item_id,
        oi.product_id AS candidate_product_id,
        oi.package_id AS candidate_package_id,
        CASE
            WHEN b.cohort_product_id IS NOT NULL
             AND oi.product_id = b.cohort_product_id
             AND (
                    b.group_id IS NULL
                 OR b.group_package_id IS NULL
                 OR oi.package_id = b.group_package_id
             )
            THEN 1 ELSE 0
        END AS strict_match
    FROM base b
    LEFT JOIN order_items oi ON oi.order_id = b.order_id
),
agg AS (
    SELECT
        b.enrollment_id,
        b.order_id,
        b.cohort_id,
        b.cohort_product_id,
        b.group_id,
        b.group_package_id,
        b.order_exists_id,
        b.cohort_exists_id,
        b.group_exists_id,
        COUNT(c.candidate_order_item_id) AS candidate_count,
        COALESCE(SUM(c.strict_match), 0) AS strict_match_count,
        COALESCE(
            JSONB_AGG(
                JSONB_BUILD_OBJECT(
                    'orderItemId', c.candidate_order_item_id,
                    'productId', c.candidate_product_id,
                    'packageId', c.candidate_package_id,
                    'strictMatch', c.strict_match = 1
                )
                ORDER BY c.candidate_order_item_id
            ) FILTER (WHERE c.candidate_order_item_id IS NOT NULL),
            '[]'::jsonb
        ) AS candidate_items
    FROM base b
    LEFT JOIN candidates c ON c.enrollment_id = b.enrollment_id
    GROUP BY
        b.enrollment_id,
        b.order_id,
        b.cohort_id,
        b.cohort_product_id,
        b.group_id,
        b.group_package_id,
        b.order_exists_id,
        b.cohort_exists_id,
        b.group_exists_id
),
classified AS (
    SELECT
        *,
        CASE
            WHEN order_id IS NULL
              OR order_exists_id IS NULL
              OR (group_id IS NOT NULL AND group_exists_id IS NULL)
              OR candidate_count = 0
            THEN 'D'
            WHEN cohort_id IS NULL
              OR cohort_exists_id IS NULL
              OR cohort_product_id IS NULL
            THEN 'E'
            WHEN candidate_count = 1
            THEN 'A'
            WHEN candidate_count > 1
             AND strict_match_count = 1
            THEN 'B'
            ELSE 'C'
        END AS classification
    FROM agg
)
SELECT
    enrollment_id AS "enrollmentId",
    order_id AS "currentOrderId",
    cohort_id AS "cohortId",
    cohort_product_id AS "cohortProductId",
    group_id AS "groupId",
    group_package_id AS "groupPackageId",
    candidate_items AS "candidateItems",
    classification
FROM classified
WHERE classification IN ('C', 'D', 'E')
ORDER BY classification, enrollment_id;

WITH base AS (
    SELECT
        e."Id" AS enrollment_id,
        e."OrderId" AS order_id,
        e."CohortId" AS cohort_id,
        e."GroupId" AS group_id,
        c."ProductId" AS cohort_product_id,
        g."CohortId" AS group_cohort_id,
        g."PackageId" AS group_package_id,
        gp.product_id AS group_package_product_id
    FROM enrollments e
    LEFT JOIN cohorts c ON c."Id" = e."CohortId"
    LEFT JOIN groups g ON g."Id" = e."GroupId"
    LEFT JOIN packages gp ON gp.id = g."PackageId"
),
candidates AS (
    SELECT
        b.enrollment_id,
        oi."Id" AS candidate_order_item_id,
        oi.product_id AS candidate_product_id,
        oi.package_id AS candidate_package_id
    FROM base b
    LEFT JOIN order_items oi
        ON oi.order_id = b.order_id
)
SELECT
    b.enrollment_id AS "enrollmentId",
    b.cohort_id AS "cohortId",
    b.cohort_product_id AS "cohortProductId",
    b.group_id AS "groupId",
    b.group_cohort_id AS "groupCohortId",
    b.group_package_id AS "groupPackageId",
    b.group_package_product_id AS "groupPackageProductId",
    c.candidate_order_item_id AS "candidateOrderItemId",
    c.candidate_product_id AS "candidateProductId",
    c.candidate_package_id AS "candidatePackageId",
    CASE
        WHEN b.group_id IS NULL THEN 'no_group'
        WHEN b.group_package_id IS NULL THEN 'group_package_missing'
        WHEN b.cohort_id IS NOT NULL AND b.group_cohort_id IS DISTINCT FROM b.cohort_id THEN 'group_not_in_enrollment_cohort'
        WHEN b.cohort_product_id IS NOT NULL AND b.group_package_product_id IS DISTINCT FROM b.cohort_product_id THEN 'group_package_not_in_cohort_product'
        WHEN c.candidate_order_item_id IS NOT NULL AND c.candidate_package_id IS DISTINCT FROM b.group_package_id THEN 'candidate_package_mismatch'
        ELSE 'ok'
    END AS issue
FROM base b
LEFT JOIN candidates c
    ON c.enrollment_id = b.enrollment_id
WHERE
       b.group_id IS NOT NULL
   AND (
           b.group_package_id IS NULL
        OR (b.cohort_id IS NOT NULL AND b.group_cohort_id IS DISTINCT FROM b.cohort_id)
        OR (b.cohort_product_id IS NOT NULL AND b.group_package_product_id IS DISTINCT FROM b.cohort_product_id)
        OR (c.candidate_order_item_id IS NOT NULL AND c.candidate_package_id IS DISTINCT FROM b.group_package_id)
   )
ORDER BY b.enrollment_id, c.candidate_order_item_id;
