﻿using System.Security.Claims;
using CoreService.Application.Dto;
using CoreService.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoreService.Api.Controllers
{  
    [ApiController]
    [Route("api/auth")]
    public class AuthController: ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService service)
        {
            _authService = service;
        }

        /// <summary>
        /// Registers a new user.
        /// </summary>
        /// <param name="userForRegistration">User registration details.</param>
        /// <returns>HTTP 201 status code if the registration is successful; otherwise, returns a 400 status code with validation errors.</returns>
        /// <response code="201">User registered successfully.</response>
        /// <response code="400">Registration failed due to validation errors.</response>
        [HttpPost("register")]
        public async Task<IActionResult> RegisterUser([FromBody] UserForRegistrationDto userForRegistration)
        {
            var result = await _authService.RegisterUser(userForRegistration);
            if (result.Succeeded) return StatusCode(201);
            
            foreach (var error in result.Errors)
            {
                ModelState.TryAddModelError(string.Empty, error);
            }
            return BadRequest(ModelState);
        }

        /// <summary>
        /// Authenticates a user and generates access and refresh tokens.
        /// </summary>
        /// <param name="userDto">User authentication details.</param>
        /// <returns>HTTP 200 status code with tokens if authentication is successful; otherwise, returns a 401 status code if authentication fails.</returns>
        /// <response code="200">Authentication successful, access + refresh tokens returned.</response>
        /// <response code="401">Authentication failed.</response>
        [HttpPost("login")]
        public async Task<IActionResult> Authenticate([FromBody] UserForAuthenticationDto userDto)
        {
            var (user, result) = await _authService.ValidateUser(userDto);
            if (!result)
                return Unauthorized();

            var tokenDto = await _authService.CreateTokens(user);
            return Ok(tokenDto);
        }
        /// <summary>
        /// Refreshes the access token using the provided refresh token.
        /// </summary>
        /// <param name="tokenDto">Current access and refresh tokens.</param>
        /// <returns>HTTP 200 status code with updated tokens if refresh is successful; otherwise, returns a 400 status code if the tokens are invalid.</returns>
        /// <response code="200">Access token refreshed successfully.</response>
        /// <response code="400">Token refresh failed due to invalid tokens.</response>
        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] TokenDto tokenDto)
        {
            var tokenDtoToReturn = await _authService.RefreshAccessToken(tokenDto);
            return Ok(tokenDtoToReturn);
        }
        
        /// <summary>
        /// Revokes the refresh token using the provided access token.
        /// </summary>
        /// <returns>HTTP 200 status code if revoke is successful; otherwise, returns a 401 status code if authentication fails.</returns>
        /// <response code="200">Refresh token revoked successfully.</response>
        /// <response code="401">Authentication failed.</response>
        [HttpPost("revoke")]
        [Authorize]
        public async Task<IActionResult> Revoke()
        {
            var userId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized("User authentication required.");
            
            await _authService.RevokeRefreshToken(userId);
            return Ok();
        }
        
        //Todo: change password with revoking tokens
    }
}
