// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Yarp.Sample.Controllers
{
    [AllowAnonymous]
    [Route("[controller]/[action]")]
    public class AccountController : Controller
    {
        [HttpGet]
        public IActionResult Login(string returnUrl)
        {
            ViewData["returnUrl"] = returnUrl;

            return View();
        }

        // Processes input from the Login.cshtml page to authenticate the user
        [HttpPost]
        public IActionResult Login(string name, string myClaimValue, string returnUrl)
        {
            // Create a new identity with 2 claims based on the fields in the form
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, name),
                new Claim("myCustomClaim", myClaimValue)
            }, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            return SignIn(principal, new AuthenticationProperties()
            {
                RedirectUri = returnUrl
                // SignIn is the only one that requires a scheme: https://github.com/dotnet/aspnetcore/issues/23325
            }, CookieAuthenticationDefaults.AuthenticationScheme);
        }

        [HttpPost]
        public IActionResult Logout()
        {
            return SignOut(new AuthenticationProperties()
            {
                RedirectUri = "/Account/LoggedOut",
            });
        }

        [HttpGet]
        public IActionResult LoggedOut()
        {
            return View();
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
