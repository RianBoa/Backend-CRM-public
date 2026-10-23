using backend.Domain.Users;

namespace backend.Auth;

public static class AuthPolicies
{
    public const string ManageClients = "manage_clients";
    public const string ManageOrders = "manage_orders";
    public const string ManageProducts = "manage_products";
    public const string ManageSubscriptions = "manage_subscriptions";
    public const string ManageApplications = "manage_applications";
    public const string ManageCases = "manage_cases";
    public const string ManageLearningStructure = "manage_learning_structure";
    public const string ManageRoadMaps = "manage_roadmaps";
    public const string ManageUsers = "manage_users";
    public const string ViewFinancials = "view_financials";
    public const string ManageFinancials = "manage_financials";

    public static IReadOnlyList<string> All { get; } =
    [
        ManageClients,
        ManageOrders,
        ManageProducts,
        ManageSubscriptions,
        ManageApplications,
        ManageCases,
        ManageLearningStructure,
        ManageRoadMaps,
        ManageUsers,
        ViewFinancials,
        ManageFinancials
    ];

    public static IReadOnlyList<string> GetPermissions(User.UserRole role)
        => role switch
        {
            User.UserRole.Admin =>
            [
                ManageClients,
                ManageOrders,
                ManageProducts,
                ManageSubscriptions,
                ManageApplications,
                ManageCases,
                ManageLearningStructure,
                ManageRoadMaps,
                ManageUsers,
                ViewFinancials,
                ManageFinancials
            ],
            User.UserRole.OnBoarding =>
            [
                ManageClients,
                ManageOrders,
                ManageApplications,
                ManageLearningStructure
            ],
            User.UserRole.Curator =>
            [
                ManageClients,
                ManageOrders,
                ManageSubscriptions,
                ManageApplications,
                ManageCases,
                ManageLearningStructure,
                ManageRoadMaps
            ],
            User.UserRole.ProjectManager =>
            [
                ManageClients,
                ManageOrders,
                ManageProducts,
                ManageSubscriptions,
                ManageApplications,
                ManageCases,
                ManageLearningStructure,
                ManageRoadMaps,
                ManageUsers,
                ViewFinancials
            ],
            User.UserRole.TroubleShooter =>
            [
                ManageClients,
                ManageSubscriptions,
                ManageApplications,
                ManageCases
            ],
            User.UserRole.Analyst =>
            [
                ManageCases
            ],
            User.UserRole.TeamLead =>
            [
                ManageClients,
                ManageOrders,
                ManageProducts,
                ManageSubscriptions,
                ManageApplications,
                ManageCases,
                ManageLearningStructure,
                ManageRoadMaps,
                ManageUsers,
                ViewFinancials
            ],
            User.UserRole.Developer =>
            [
                ManageProducts
            ],
            _ => []
        };
}
