using FluentAssertions;
using Tabu.Domain.Entities;
using Xunit;

namespace Tabu.Application.Tests.Services;

/// <summary>
/// Guards the numeric mapping of <see cref="ClockSize"/>. The UI binds
/// <c>FontSize = (int)ClockSize</c> directly, so changing any of these
/// values silently would resize the clock across every installed build.
/// </summary>
public sealed class ClockSizeTests
{
    [Theory]
    [InlineData(ClockSize.Small, 11)]
    [InlineData(ClockSize.Medium, 14)]
    [InlineData(ClockSize.Large, 18)]
    public void EnumValue_MapsToExpectedFontSize(ClockSize size, int expected)
    {
        ((int)size).Should().Be(expected);
    }
}
