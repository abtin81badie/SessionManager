using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SessionManager.Api.Controllers;
using SessionManager.Application.DTOs;
using SessionManager.Application.Interfaces;
using SessionManager.Domain.Entities;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Xunit;

namespace SessionManager.Tests
{
    public class SessionsControllerTests
    {
        private readonly Mock<ISessionRepository> _mockSessionRepo;
        private readonly SessionsController _controller;

        public SessionsControllerTests()
        {
            _mockSessionRepo = new Mock<ISessionRepository>();
            _controller = new SessionsController(_mockSessionRepo.Object);
        }

        // --- HELPER: Generate Fake JWT ---
        private string GenerateTestJwt(string? jti = null, string? sub = null)
        {
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, sub ?? "user-123"), // Default if null
                new Claim("role", "User")
            };

            if (jti != null)
            {
                claims.Add(new Claim(JwtRegisteredClaimNames.Jti, jti));
            }

            var token = new JwtSecurityToken(claims: claims);
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        // --- HELPER: Setup HttpContext ---
        private void SetupHttpContext(string token)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["Authorization"] = $"Bearer {token}";
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };
        }

        [Fact]
        public async Task RenewSession_Should_ReturnOk_WhenTokenIsValidAndSessionExists()
        {
            // Arrange
            var sessionId = Guid.NewGuid().ToString();
            var userId = Guid.NewGuid();
            var token = GenerateTestJwt(jti: sessionId, sub: userId.ToString());

            SetupHttpContext(token);

            // Mock Session Lookup
            _mockSessionRepo.Setup(x => x.GetSessionAsync(sessionId))
                .ReturnsAsync(new SessionInfo { Token = sessionId, UserId = userId });

            // Act
            var result = await _controller.RenewSession();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<RenewSessionResponse>(okResult.Value);

            Assert.Equal("Session renewed successfully.", response.Message);
            _mockSessionRepo.Verify(x => x.ExtendSessionAsync(userId, sessionId, It.IsAny<TimeSpan>()), Times.Once);
        }

    }
}