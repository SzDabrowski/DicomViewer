using Avalonia;
using DicomViewer.Helpers;
using Xunit;

namespace DicomViewer.Tests.Unit;

public class GeometryHelperTests
{
    [Fact]
    public void PointOnSegment_ReturnsZero()
    {
        var a = new Point(0, 0);
        var b = new Point(10, 0);
        var p = new Point(5, 0);

        double distance = GeometryHelper.DistanceToSegment(p, a, b);

        Assert.Equal(0, distance, precision: 5);
    }

    [Fact]
    public void PointPerpendicularToMidpoint_ReturnsPerpendicularDistance()
    {
        var a = new Point(0, 0);
        var b = new Point(10, 0);
        var p = new Point(5, 3);

        double distance = GeometryHelper.DistanceToSegment(p, a, b);

        Assert.Equal(3.0, distance, precision: 5);
    }

    [Fact]
    public void PointClosestToEndpointA_ReturnsDistanceToA()
    {
        var a = new Point(0, 0);
        var b = new Point(10, 0);
        var p = new Point(-3, 4);

        double distance = GeometryHelper.DistanceToSegment(p, a, b);

        Assert.Equal(5.0, distance, precision: 5);
    }

    [Fact]
    public void PointClosestToEndpointB_ReturnsDistanceToB()
    {
        var a = new Point(0, 0);
        var b = new Point(10, 0);
        var p = new Point(13, 4);

        double distance = GeometryHelper.DistanceToSegment(p, a, b);

        Assert.Equal(5.0, distance, precision: 5);
    }

    [Fact]
    public void DegenerateSegment_ReturnsDistanceToPoint()
    {
        var a = new Point(3, 4);
        var b = new Point(3, 4);
        var p = new Point(0, 0);

        double distance = GeometryHelper.DistanceToSegment(p, a, b);

        Assert.Equal(5.0, distance, precision: 5);
    }

    [Fact]
    public void PointOnEndpointA_ReturnsZero()
    {
        var a = new Point(2, 3);
        var b = new Point(8, 7);

        double distance = GeometryHelper.DistanceToSegment(a, a, b);

        Assert.Equal(0, distance, precision: 5);
    }

    [Fact]
    public void PointOnEndpointB_ReturnsZero()
    {
        var a = new Point(2, 3);
        var b = new Point(8, 7);

        double distance = GeometryHelper.DistanceToSegment(b, a, b);

        Assert.Equal(0, distance, precision: 5);
    }
}
