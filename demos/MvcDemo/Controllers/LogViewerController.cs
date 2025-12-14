using MvcDemo.Utilities;
using System;
using System.Web.Mvc;

namespace MvcDemo.Controllers
{
    [AllowAnonymous]
    public class LogViewerController : Controller
    {
        public ActionResult Index()
        {
            var logs = TraceLogBuffer.Instance.GetLogs();
            return View(logs);
        }

        public ActionResult Clear()
        {
            TraceLogBuffer.Instance.Clear();
            return RedirectToAction("Index");
        }

        [HttpGet]
        public ActionResult GetLogs(string since = null)
        {
            // If a 'since' timestamp is provided, filter to only include logs newer than that timestamp
            if (!string.IsNullOrEmpty(since))
            {
                // Try parsing as ISO format first, then fall back to other formats
                DateTime sinceDateTime;
                if (DateTime.TryParse(since, System.Globalization.CultureInfo.InvariantCulture, 
                    System.Globalization.DateTimeStyles.RoundtripKind, out sinceDateTime))
                {
                    var logs = TraceLogBuffer.Instance.GetLogsSince(sinceDateTime);
                    // Map to anonymous objects to ensure FormattedTimestamp is included in JSON
                    var mappedLogs = logs.ConvertAll(log => new
                    {
                        timestamp = log.Timestamp,
                        message = log.Message,
                        level = log.Level,
                        formattedTimestamp = log.FormattedTimestamp
                    });
                    return Json(new { logs = mappedLogs }, JsonRequestBehavior.AllowGet);
                }
            }

            // Otherwise, return all logs
            var allLogs = TraceLogBuffer.Instance.GetLogs();
            // Map to anonymous objects to ensure FormattedTimestamp is included in JSON
            var mappedAllLogs = allLogs.ConvertAll(log => new
            {
                timestamp = log.Timestamp,
                message = log.Message,
                level = log.Level,
                formattedTimestamp = log.FormattedTimestamp
            });
            return Json(new { logs = mappedAllLogs }, JsonRequestBehavior.AllowGet);
        }
    }
}
