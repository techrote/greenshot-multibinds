using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapplo.Ini;
using Greenshot.Base;
using Greenshot.Base.Core;
using Greenshot.Base.Interfaces;
using Greenshot.Settings.ViewModels;
using Xunit;

namespace Greenshot.Tests.Core
{
    // These tests temporarily register destinations and change the shared configuration.
    [CollectionDefinition("DestinationPickerVisibility", DisableParallelization = true)]
    public class DestinationPickerVisibilityCollection
    {
    }

    [Collection("DestinationPickerVisibility")]
    public class DestinationPickerVisibilityTests
    {
        public DestinationPickerVisibilityTests()
        {
            TestEnvironment.EnsureInitialized();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void UnsetOrEmptyList_ShowsAvailableDestinations(string entry)
        {
            WithDestination(destination =>
            {
                var configuration = new CoreConfigurationImpl();
                configuration.ResetToDefaults();
                configuration.HiddenPickerDestinations = entry == null ? null : new List<string> { entry };

                Assert.Contains(destination, DestinationHelper.GetPickerDestinations(configuration, null));
            });
        }

        [Fact]
        public void HiddenDestination_IsStillAvailableForDirectExport_AndCanBeRestored()
        {
            WithDestination(destination =>
            {
                var configuration = new CoreConfigurationImpl();
                configuration.ResetToDefaults();
                configuration.HiddenPickerDestinations = new List<string> { "  " + destination.Designation.ToLowerInvariant() + "  " };

                Assert.DoesNotContain(destination, DestinationHelper.GetPickerDestinations(configuration, null));
                Assert.Contains(destination, DestinationHelper.GetAllDestinations());
                Assert.Same(destination, DestinationHelper.GetDestination(destination.Designation));
                Assert.True(destination.IsAvailableFor(null));
                Assert.True(ConfigurationRestrictions.IsDestinationAllowed(configuration, destination.Designation));

                configuration.HiddenPickerDestinations.Clear();
                Assert.Contains(destination, DestinationHelper.GetPickerDestinations(configuration, null));
                destination.Available = false;
                Assert.DoesNotContain(destination, DestinationHelper.GetPickerDestinations(configuration, null));
            });
        }

        [Fact]
        public void HidingAllDestinations_ProducesAnEmptyChoiceList()
        {
            WithDestination(destination =>
            {
                var configuration = new CoreConfigurationImpl();
                configuration.ResetToDefaults();
                configuration.HiddenPickerDestinations = DestinationHelper.GetAllDestinations().Select(item => item.Designation).ToList();

                Assert.Empty(DestinationHelper.GetPickerDestinations(configuration, null));
            });
        }

        [Fact]
        public void Settings_SaveAndReopen_PreservesMissingDestinations_WithoutDuplicateRows()
        {
            WithDestination(destination =>
            {
                var configuration = IniConfigRegistry.GetSection<ICoreConfiguration>();
                var previousHidden = configuration.HiddenPickerDestinations;
                var previousExcluded = configuration.ExcludeDestinations;
                var previousOutputs = configuration.OutputDestinations;
                try
                {
                    configuration.HiddenPickerDestinations = new List<string> { destination.Designation, "MissingPluginDestination" };
                    var viewModel = new SettingsViewModel();
                    var row = Assert.Single(viewModel.Destinations, item => item.Designation == destination.Designation);
                    Assert.False(row.IsShownInPicker);
                    Assert.True(row.IsAllowed);
                    row.IsSelected = true;
                    row.IsShownInPicker = true;
                    viewModel.SaveDestinationPickerVisibility();

                    Assert.True(row.IsSelected);
                    Assert.Equal(new[] { "MissingPluginDestination" }, configuration.HiddenPickerDestinations);
                    Assert.Same(previousExcluded, configuration.ExcludeDestinations);
                    Assert.Same(previousOutputs, configuration.OutputDestinations);

                    var reopened = new SettingsViewModel();
                    Assert.True(Assert.Single(reopened.Destinations, item => item.Designation == destination.Designation).IsShownInPicker);
                    Assert.False(Assert.Single(reopened.Destinations, item => item.Designation == "MissingPluginDestination").IsShownInPicker);
                    reopened.Destinations.Single(item => item.Designation == destination.Designation).IsShownInPicker = false;
                    reopened.SaveDestinationPickerVisibility();
                    Assert.Contains(destination.Designation, configuration.HiddenPickerDestinations);
                }
                finally
                {
                    configuration.HiddenPickerDestinations = previousHidden;
                }
            });
        }

        private static void WithDestination(Action<TestDestination> test)
        {
            var destination = new TestDestination();
            SimpleServiceProvider.Current.AddService<IDestination>(destination);
            try
            {
                test(destination);
            }
            finally
            {
                SimpleServiceProvider.Current.RemoveService<IDestination>(new[] { destination });
            }
        }

        private sealed class TestDestination : DestinationBase
        {
            public bool Available { get; set; } = true;
            public override string Designation => "PickerVisibilityTestPrinter";
            public override DestinationDescriptor Descriptor => new DestinationDescriptor("Test printer");
            public override bool IsAvailableFor(ICaptureDetails metadata) => Available;
            public override Task<ExportResult> ExportAsync(ExportRequest request, CancellationToken cancellationToken) => Task.FromResult(ExportResult.Succeeded());
        }
    }
}
