using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SessionManager.Api.Controllers;
using SessionManager.Application.DTOs;
using SessionManager.Application.Interfaces;
using SessionManager.Domain.Entities;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Xunit;

namespace SessionManager.Tests
{
    public class AdminControllerTests
    {
        private readonly Mock<ISessionRepository> _mockSessionRepo;
        private readonly AdminController _controller;

        public AdminControllerTests()
        {
            _mockSessionRepo = new Mock<ISessionRepository>();
            _controller = new AdminController(_mockSessionRepo.Object);
        }

        // --- HELPER: Generate Fake JWT ---
        private string GenerateTestJwt(string role, string jti, string sub)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Role, role),
                new Claim(JwtRegisteredClaimNames.Jti, jti),
                new Claim(JwtRegisteredClaimNames.Sub, sub)
            };

            var token = new JwtSecurityToken(claims: claims);
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        // --- HELPER: Setup Controller Context ---
        private void SetupControllerContext(string token)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["Authorization"] = $"Bearer {token}";
            _controller.ControllerContext = new ControllerContext()
            {
                HttpContext = httpContext
            };
        }

        [Fact]
        public async Task GetSessionStats_Admin_ShouldCallRepoWithNull()
        {
            // Arrange
            var sessionId = Guid.NewGuid().ToString();
            var userId = Guid.NewGuid();
            var token = GenerateTestJwt("Admin", sessionId, userId.ToString());

            SetupControllerContext(token);

            // Mock Session Existence Check (Critical step in your new logic)
            _mockSessionRepo.Setup(x => x.GetSessionAsync(sessionId))
                .ReturnsAsync(new SessionInfo { Token = sessionId });

            // Mock Stats Return
            _mockSessionRepo.Setup(x => x.GetSessionStatsAsync(null))
                .ReturnsAsync(new SessionStatsDto { TotalActiveSessions = 10 });

            // Act
            var result = await _controller.GetSessionStats();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<SessionStatsResponse>(okResult.Value);

            // Verify Admin Logic: Called with NULL
            _mockSessionRepo.Verify(x => x.GetSessionStatsAsync(null), Times.Once);
            Assert.Equal(10, response.TotalActiveSessions);
        }

        [Fact]
        public async Task GetSessionStats_User_ShouldCallRepoWithUserId()
        {
            // Arrange
            var sessionId = Guid.NewGuid().ToString();
            var userId = Guid.NewGuid();
            var token = GenerateTestJwt("User", sessionId, userId.ToString());

            SetupControllerContext(token);

            // Mock Session Existence
            _mockSessionRepo.Setup(x => x.GetSessionAsync(sessionId))
                .ReturnsAsync(new SessionInfo { Token = sessionId });

            // Mock Stats Return
            _mockSessionRepo.Setup(x => x.GetSessionStatsAsync(userId))
                .ReturnsAsync(new SessionStatsDto { TotalActiveSessions = 1 });

            // Act
            var result = await _controller.GetSessionStats();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);

            // Verify User Logic: Called with specific UserId
            _mockSessionRepo.Verify(x => x.GetSessionStatsAsync(userId), Times.Once);
        }

        [Fact]
        public async Task GetSessionStats_ShouldReturnUnauthorized_WhenSessionIsRevoked()
        {
            // Arrange
            var sessionId = Guid.NewGuid().ToString();
            var userId = Guid.NewGuid();
            var token = GenerateTestJwt("User", sessionId, userId.ToString());

            SetupControllerContext(token);

            // Mock Session Lookup returns NULL (Session Revoked/Expired)
            _mockSessionRepo.Setup(x => x.GetSessionAsync(sessionId))
                .ReturnsAsync((SessionInfo?)null);

            // Act
            var result = await _controller.GetSessionStats();

            // Assert
            var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);

            // Verify we never even tried to fetch stats
            _mockSessionRepo.Verify(x => x.GetSessionStatsAsync(It.IsAny<Guid?>()), Times.Never);
        }
    }
}