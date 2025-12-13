using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web.Mvc;
using MvcDemo.Models;
using MvcDemo.Services;

namespace MvcDemo.Controllers
{
    /// <summary>
    /// Controller for SAML token validation.
    /// Allows users to paste SAML tokens and validate them against issuer metadata.
    /// </summary>
    [AllowAnonymous]
    public class TokenValidatorController : Controller
    {
        /// <summary>
        /// Displays the token validator form.
        /// </summary>
        public ActionResult Index()
        {
            var issuers = IssuerManagementService.GetCurrentIssuers();
            ViewBag.Issuers = issuers;
            return View(new TokenValidatorViewModel());
        }

        /// <summary>
        /// Validates a SAML token (POST).
        /// DisableRequestValidation is required to accept XML content in the SAML token field.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public async Task<ActionResult> Validate(TokenValidatorViewModel model)
        {
            var issuers = IssuerManagementService.GetCurrentIssuers();
            ViewBag.Issuers = issuers;

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Please fill in all required fields";
                return RedirectToAction("Index");
            }

            if (string.IsNullOrWhiteSpace(model.IssuerId) || string.IsNullOrWhiteSpace(model.SamlToken))
            {
                TempData["ErrorMessage"] = "Issuer and SAML token are required";
                return RedirectToAction("Index");
            }

            try
            {
                // Validate the token
                var validationResult = await TokenValidationService.ValidateSamlTokenAsync(model.IssuerId, model.SamlToken);

                return View("Result", validationResult);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError(
                    string.Format("TokenValidatorController: Error during token validation: {0}", ex));
                
                TempData["ErrorMessage"] = string.Format("An error occurred during token validation: {0}", ex.Message);
                return RedirectToAction("Index");
            }
        }

        /// <summary>
        /// Returns to the validator form.
        /// </summary>
        public ActionResult Back()
        {
            return RedirectToAction("Index");
        }
    }
}
