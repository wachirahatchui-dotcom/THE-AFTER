using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Authentication.Internal;
using Unity.Services.Core.Configuration.Internal;
using Unity.Services.Core.Internal;
using Unity.Services.Core.Telemetry.Internal;
using Unity.Services.Wire.Internal;
using UnityEngine;

namespace Unity.Services.Friends
{
    /// <summary>
    /// FriendsPackageInitializer register the friends component and modifies the configuration based on the
    /// project's configuration.
    /// </summary>
    public class FriendsPackageInitializer : IInitializablePackage
    {
        /// <summary>
        /// The full name of the Friends package.
        /// </summary>
        const string k_PackageName = "com.unity.services.friends";

        /// <summary>
        /// Key specified when switching the friends cloud environment.
        /// </summary>
        const string k_CloudEnvironmentKey = "com.unity.services.core.cloud-environment";

        /// <summary>
        /// Staging environment value when switching the friends cloud environment.
        /// </summary>
        const string k_StagingEnvironment = "staging";

        /// <summary>
        /// Register the friends package in the core registry.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            CoreRegistry.Instance.RegisterPackage(new FriendsPackageInitializer())
                .DependsOn<IAccessToken>()
                .DependsOn<IProjectConfiguration>()
                .DependsOn<IWire>()
                .DependsOn<IMetricsFactory>();
        }

        /// <summary>
        /// Async Operation.
        /// Initialize the friends package based on different components.
        /// </summary>
        /// <param name="registry">A container to store all available <see cref="IInitializablePackage"/>.</param>
        /// <returns>Awaitable task</returns>
        public Task Initialize(CoreRegistry registry)
        {
            WrappedFriendsApi.s_Configuration.BasePath =
                GetBasePath(registry.GetServiceComponent<IProjectConfiguration>());
            FriendsApi.s_Wire = registry.GetServiceComponent<IWire>();
            FriendsService.s_Wire = registry.GetServiceComponent<IWire>();
            AuthenticationService.Instance.SignedOut += FriendsService.ClearService;

            var metricsFactory = registry.GetServiceComponent<IMetricsFactory>();
            var metrics = metricsFactory.Create(k_PackageName);
            FriendsApi.s_Metrics = metrics;
            FriendsService.s_Metrics = metrics;

            return Task.CompletedTask;
        }

        /// <summary>
        /// Get the friends base path to connect to when doing API calls.
        /// </summary>
        /// <param name="projectConfiguration">Component for project configuration.</param>
        /// <returns>Return the base path for the friends API configuration.</returns>
        static string GetBasePath(IProjectConfiguration projectConfiguration)
        {
            var cloudEnvironment = projectConfiguration?.GetString(k_CloudEnvironmentKey);

            switch (cloudEnvironment)
            {
                case k_StagingEnvironment:
                    return "https://social-stg.services.api.unity.com";
                default:
                    return "https://social.services.api.unity.com";
            }
        }
    }
}
