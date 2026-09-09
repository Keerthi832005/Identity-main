using Identity.Domain.Entities;
using Identity.Domain.Enums;

namespace Identity.Domain.Tests;

public sealed class OrganizationUnitTests
{
    [Theory]
    [InlineData(OrganizationUnitType.Country, OrganizationUnitType.Organization)]
    [InlineData(OrganizationUnitType.Region, OrganizationUnitType.Country)]
    [InlineData(OrganizationUnitType.State, OrganizationUnitType.Region)]
    [InlineData(OrganizationUnitType.Branch, OrganizationUnitType.State)]
    [InlineData(OrganizationUnitType.Location, OrganizationUnitType.Branch)]
    [InlineData(OrganizationUnitType.Department, OrganizationUnitType.Organization)]
    [InlineData(OrganizationUnitType.Team, OrganizationUnitType.Department)]
    public void ParentType_FollowsApprovedHierarchy(OrganizationUnitType child, OrganizationUnitType expected) =>
        Assert.Equal(expected, OrganizationUnit.ParentType(child));

    [Fact]
    public void Root_IsNormalizedHasNoParentAndRejectsInvalidDetails()
    {
        var root = OrganizationUnit.CreateRoot(7, " ROOT ", " Organization ", DateTime.UtcNow);
        Assert.Equal("ROOT", root.UnitCode);
        Assert.Equal("Organization", root.UnitName);
        Assert.Null(root.ParentOrganizationUnitId);
        Assert.Null(OrganizationUnit.ParentType(OrganizationUnitType.Organization));
        Assert.Throws<ArgumentOutOfRangeException>(() => OrganizationUnit.CreateRoot(0, "ROOT", "Organization", DateTime.UtcNow));
        Assert.Throws<ArgumentException>(() => Country.Create(root));
        Assert.Throws<ArgumentException>(() => OrganizationUnit.CreateChild(7, root, OrganizationUnitType.Country, "IN", "India", DateTime.UtcNow));
        Assert.Throws<ArgumentOutOfRangeException>(() => root.SetAddress(new OrganizationUnitAddress(Latitude: 91), DateTime.UtcNow));
        Assert.Throws<ArgumentOutOfRangeException>(() => root.SetAddress(new OrganizationUnitAddress(Longitude: -181), DateTime.UtcNow));
    }
}
