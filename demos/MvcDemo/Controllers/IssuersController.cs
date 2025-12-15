using IdentityMetadataFetcher.Iis.Configuration;
using MvcDemo.Models;
using MvcDemo.Services;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web.Mvc;

namespace MvcDemo.Controllers
{
    [AllowAnonymous]
    public class IssuersController : Controller
    {
        /// <summary>
        /// Displays a list of all issuers currently being monitored by the polling service.
        /// </summary>
        public ActionResult Index()
        {
            var issuers = IssuerManagementService.GetCurrentIssuers();
            return View(issuers);
        }

        /// <summary>
        /// Displays the form to create a new issuer.
        /// </summary>
        public ActionResult Create()
        {
            return View(new IssuerViewModel());
        }

        /// <summary>
        /// Processes the creation of a new issuer (POST).
        /// Adds the issuer to the running polling service immediately.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(IssuerViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Validate endpoint URL
            if (!Uri.TryCreate(model.Endpoint, UriKind.Absolute, out var uriResult) ||
                (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps))
            {
                ModelState.AddModelError(nameof(model.Endpoint), "Please enter a valid HTTP or HTTPS URL.");
                return View(model);
            }

            // Check if issuer already exists
            var existing = IssuerManagementService.GetCurrentIssuers();
            if (existing.Any(i => i.Id == model.Id))
            {
                ModelState.AddModelError(nameof(model.Id), "An issuer with this ID already exists.");
                return View(model);
            }

            try
            {
                // Add issuer to the running polling service
                if (IssuerManagementService.AddIssuer(model))
                {
                    System.Diagnostics.Trace.TraceInformation(
                        $"IssuersController: Create issuer - ID: {model.Id}, Name: {model.Name}, Endpoint: {model.Endpoint}");

                    TempData["SuccessMessage"] = $"Issuer '{model.Name}' has been added and is now being monitored.";
                    return RedirectToAction("Index");
                }
                else
                {
                    ModelState.AddModelError("", "Failed to add issuer to the polling service.");
                    return View(model);
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error creating issuer: {ex.Message}");
                System.Diagnostics.Trace.TraceError($"IssuersController: Error creating issuer: {ex.Message}");
                return View(model);
            }
        }

        /// <summary>
        /// Displays the form to edit an existing issuer.
        /// </summary>
        public ActionResult Edit(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return RedirectToAction("Index");
            }

            var issuers = IssuerManagementService.GetCurrentIssuers();
            var issuer = issuers.FirstOrDefault(i => i.Id == id);

            if (issuer == null)
            {
                TempData["ErrorMessage"] = $"Issuer with ID '{id}' not found.";
                return RedirectToAction("Index");
            }

            // Convert IssuerDetailViewModel to IssuerViewModel for editing
            var editModel = new IssuerViewModel
            {
                Id = issuer.Id,
                Name = issuer.Name,
                Endpoint = issuer.Endpoint
            };

            return View(editModel);
        }

        /// <summary>
        /// Processes the update of an existing issuer (POST).
        /// Updates the issuer in the running polling service immediately.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(string id, IssuerViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (model.Id != id)
            {
                ModelState.AddModelError("", "Issuer ID mismatch.");
                return View(model);
            }

            // Validate endpoint URL
            if (!Uri.TryCreate(model.Endpoint, UriKind.Absolute, out var uriResult) ||
                (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps))
            {
                ModelState.AddModelError(nameof(model.Endpoint), "Please enter a valid HTTP or HTTPS URL.");
                return View(model);
            }

            try
            {
                // Update issuer in the running polling service
                if (IssuerManagementService.UpdateIssuer(model))
                {
                    System.Diagnostics.Trace.TraceInformation(
                        $"IssuersController: Edit issuer - ID: {model.Id}, Name: {model.Name}, Endpoint: {model.Endpoint}");

                    TempData["SuccessMessage"] = $"Issuer '{model.Name}' has been updated.";
                    return RedirectToAction("Index");
                }
                else
                {
                    TempData["ErrorMessage"] = $"Issuer with ID '{id}' not found.";
                    return RedirectToAction("Index");
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error updating issuer: {ex.Message}");
                System.Diagnostics.Trace.TraceError($"IssuersController: Error updating issuer: {ex.Message}");
                return View(model);
            }
        }

        /// <summary>
        /// Processes the deletion of an issuer (POST).
        /// Removes the issuer from the running polling service immediately.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                TempData["ErrorMessage"] = "Invalid issuer ID.";
                return RedirectToAction("Index");
            }

            try
            {
                var issuers = IssuerManagementService.GetCurrentIssuers();
                var issuer = issuers.FirstOrDefault(i => i.Id == id);

                if (issuer == null)
                {
                    TempData["ErrorMessage"] = $"Issuer with ID '{id}' not found.";
                    return RedirectToAction("Index");
                }

                // Remove issuer from the running polling service
                if (IssuerManagementService.RemoveIssuer(id))
                {
                    System.Diagnostics.Trace.TraceInformation(
                        $"IssuersController: Delete issuer - ID: {id}");

                    TempData["SuccessMessage"] = $"Issuer '{issuer.Name}' has been removed and is no longer being monitored.";
                    return RedirectToAction("Index");
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to delete issuer.";
                    return RedirectToAction("Index");
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error deleting issuer: {ex.Message}";
                System.Diagnostics.Trace.TraceError($"IssuersController: Error deleting issuer {id}: {ex.Message}");
                return RedirectToAction("Index");
            }
        }
    }
}
