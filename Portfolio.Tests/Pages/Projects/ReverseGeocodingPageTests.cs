using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Portfolio.Web.Pages.Projects.ReverseGeocoding;

namespace Portfolio.Tests.Pages.Projects
{
    public class ReverseGeocodingPageTests
    {
        [Fact]
        public void OnGet_ReturnsPageResult()
        {
            // Arrange
            var pageModel = new IndexModel();

            // Act
            var result = pageModel.OnGet();

            // Assert
            Assert.IsType<PageResult>(result);
        }
    }
}
