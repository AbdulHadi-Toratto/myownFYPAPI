using ExcelDataReader;
using myownFYPAPI.Models;
using myownFYPAPI.Models.DTO;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Http;

namespace myownFYPAPI.Controllers.Datacell.ExcelUpload
{
    [RoutePrefix("api/CHR")]
    public class CHRController : ApiController
    {
        fypapiv1Entities db = new fypapiv1Entities();

        // ── Score Calculator ─────────────────────────────────────
        private int CalculateScore(int? lateInMinutes, int? leftEarlyMinutes = null)
        {
            int total = (lateInMinutes ?? 0) + (leftEarlyMinutes ?? 0);
            if (total >= 10) return 0;
            if (total >= 6) return 3;
            if (total >= 1) return 4;
            return 5;
        }

        private string GetComputedStatus(int? lateInMinutes, int? leftEarlyMinutes, string originalStatus)
        {
            int total = (lateInMinutes ?? 0) + (leftEarlyMinutes ?? 0);
            if (total >= 10) return "Cancelled";
            return string.IsNullOrEmpty(originalStatus) ? "Present" : originalStatus;
        }

        // ── 0. Get All Sessions (for dropdown) ───────────────────
        [HttpGet]
        [Route("GetSessions")]
        public IHttpActionResult GetSessions()
        {
            try
            {
                var sessions = db.Session
                    .OrderByDescending(s => s.id)
                    .Select(s => new
                    {
                        s.id,
                        s.name  // apne model ka actual column name use karo
                        // agar aur columns hain jaise StartDate/EndDate to woh bhi add kar sakte ho
                    })
                    .ToList();

                return Ok(sessions);
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception("Sessions Error: " + ex.Message));
            }
        }

        // ── 1. Excel Upload (Session Required) ──────────────────
        [HttpPost]
        [Route("upload")]
        public IHttpActionResult UploadCHR()
        {
            try
            {
                var httpRequest = HttpContext.Current.Request;

                // ── Session ID — form data se lo ──
                string sessionIdStr = httpRequest.Form["sessionID"];
                if (string.IsNullOrEmpty(sessionIdStr) || !int.TryParse(sessionIdStr, out int sessionID))
                    return BadRequest("Session ID required. Please select a session before uploading.");

                // Validate: session exist karti hai?
                var sessionExists = db.Session.Any(s => s.id == sessionID);
                if (!sessionExists)
                    return BadRequest("Selected session does not exist.");

                if (httpRequest.Files.Count == 0)
                    return BadRequest("No file uploaded.");

                var file = httpRequest.Files[0];
                System.Text.Encoding.RegisterProvider(
                    System.Text.CodePagesEncodingProvider.Instance);

                using (var stream = file.InputStream)
                using (var reader = ExcelReaderFactory.CreateReader(stream))
                {
                    var result = reader.AsDataSet(new ExcelDataSetConfiguration()
                    {
                        ConfigureDataTable = (_) => new ExcelDataTableConfiguration()
                        { UseHeaderRow = true }
                    });

                    DataTable dataTable = result.Tables[0];
                    int maxIdBefore = db.CHR.Any() ? db.CHR.Max(x => x.id) : 0;
                    int insertedCount = 0;

                    // ── Unique batch timestamp ──
                    DateTime uploadTime = new DateTime(
                        DateTime.Now.Year,
                        DateTime.Now.Month,
                        DateTime.Now.Day,
                        DateTime.Now.Hour,
                        DateTime.Now.Minute,
                        DateTime.Now.Second,
                        0
                    );

                    // Same second conflict avoid karo
                    bool conflict = db.CHR.Any(x => x.ClassDate == uploadTime && x.sessionID == sessionID);
                    if (conflict)
                        uploadTime = uploadTime.AddSeconds(1);

                    foreach (DataRow row in dataTable.Rows)
                    {
                        if (row[0] == DBNull.Value ||
                            string.IsNullOrWhiteSpace(row[0].ToString())) continue;

                        Func<string, string> getVal = (colName) =>
                        {
                            var col = dataTable.Columns.Cast<DataColumn>()
                                .FirstOrDefault(c => c.ColumnName.Trim()
                                    .Equals(colName, StringComparison.OrdinalIgnoreCase));
                            return (col != null && row[col] != DBNull.Value)
                                ? row[col].ToString().Trim() : "";
                        };

                        db.CHR.Add(new CHR
                        {
                            sessionID = sessionID,                    // ← Session link
                            CourseCode = getVal("CourseCode"),
                            TeacherID = getVal("TeacherID"),
                            TeacherName = getVal("TeacherName"),
                            Discipline = getVal("Discipline"),
                            Venue = getVal("Venue"),
                            Status = getVal("Status"),
                            Remarks = getVal("Remarks"),
                            LateIn = int.TryParse(getVal("LateIn"), out int late) ? late : 0,
                            LeftEarly = int.TryParse(getVal("LeftEarly"), out int left) ? left : 0,
                            ClassDate = uploadTime
                        });
                        insertedCount++;
                    }

                    db.SaveChanges();

                    // Session name bhi return karo
                    var session = db.Session.FirstOrDefault(s => s.id == sessionID);

                    return Ok(new
                    {
                        Message = "Successfully Uploaded",
                        Count = insertedCount,
                        BatchStartId = maxIdBefore + 1,
                        Date = uploadTime.ToString("dd MMM yyyy HH:mm:ss"),
                        SessionID = sessionID,
                        SessionName = session != null ? session.name : ""
                    });
                }
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, ex.ToString());
            }
        }

        // ── 2. HOD Dashboard (Session Filter Optional) ───────────
        [HttpGet]
        [Route("GetHODDashboard")]
        public IHttpActionResult GetHODDashboard(
    int? sessionID = null,
    string reportDate = null
)
        {
            try
            {
                var query = db.CHR.AsQueryable();

                // Session filter
                if (sessionID.HasValue && sessionID.Value != 0)
                    query = query.Where(c => c.sessionID == sessionID.Value);

                // Date filter
                if (!string.IsNullOrEmpty(reportDate))
                {
                    DateTime parsedDate = DateTime.Parse(reportDate).Date;

                    query = query.Where(c => DbFunctions.TruncateTime(c.ClassDate) == parsedDate);
                }

                var allRows = query.OrderBy(c => c.id).ToList();

                if (!allRows.Any())
                    return Ok(new List<object>());

                var batches = allRows
                    .GroupBy(c => new { c.ClassDate, c.sessionID })
                    .Select(g =>
                    {
                        var rows = g.OrderBy(x => x.id).ToList();
                        var first = rows.First();

                        var session = db.Session
                            .FirstOrDefault(s => s.id == first.sessionID);

                        return new
                        {
                            ReportId = first.id,
                            ReportDate = first.ClassDate,
                            Department = first.Discipline ?? "N/A",
                            SessionID = first.sessionID,
                            SessionName = session != null ? session.name : "N/A",
                            TotalClasses = rows.Count,
                            LateTeachers = rows.Count(x => (x.LateIn ?? 0) > 0),
                            EarlyLeavers = rows.Count(x => (x.LeftEarly ?? 0) > 0),
                            CancelledClasses = rows.Count(x => (x.LateIn ?? 0) >= 10),
                            AvgScore = rows.Average(x =>
                                (double)CalculateScore(x.LateIn))
                        };
                    })
                    .OrderByDescending(x => x.ReportId)
                    .ToList();

                return Ok(batches);
            }
            catch (Exception ex)
            {
                return InternalServerError(
                    new Exception("Error: " + ex.Message)
                );
            }
        }







        //old without date filter
        //[HttpGet]
        //[Route("GetHODDashboard")]
        //public IHttpActionResult GetHODDashboard(int? sessionID = null)
        //{
        //    try
        //    {
        //        // sessionID diya hai to filter karo, warna sab dikhao
        //        var query = db.CHR.AsQueryable();
        //        if (sessionID.HasValue)
        //            query = query.Where(c => c.sessionID == sessionID.Value);

        //        var allRows = query.OrderBy(c => c.id).ToList();
        //        if (!allRows.Any()) return Ok(new List<object>());

        //        var batches = allRows
        //            .GroupBy(c => new { c.ClassDate, c.sessionID })   // session + date se group
        //            .Select(g =>
        //            {
        //                var rows = g.OrderBy(x => x.id).ToList();
        //                var first = rows.First();

        //                // Session name join
        //                var session = db.Session.FirstOrDefault(s => s.id == first.sessionID);

        //                return new
        //                {
        //                    ReportId = first.id,
        //                    ReportDate = first.ClassDate,
        //                    Department = first.Discipline ?? "N/A",
        //                    SessionID = first.sessionID,
        //                    SessionName = session != null ? session.name : "N/A",
        //                    TotalClasses = rows.Count,
        //                    LateTeachers = rows.Count(x => (x.LateIn ?? 0) > 0),
        //                    EarlyLeavers = rows.Count(x => (x.LeftEarly ?? 0) > 0),
        //                    CancelledClasses = rows.Count(x => (x.LateIn ?? 0) >= 10),
        //                    AvgScore = rows.Average(x =>
        //                                       (double)CalculateScore(x.LateIn))
        //                };
        //            })
        //            .OrderByDescending(x => x.ReportId)
        //            .ToList();

        //        return Ok(batches);
        //    }
        //    catch (Exception ex)
        //    {
        //        return InternalServerError(new Exception("Error: " + ex.Message));
        //    }
        //}

        // ── 3. Detail by ReportId ────────────────────────────────
        [HttpGet]
        [Route("GetReportById/{id:int}")]
        public IHttpActionResult GetReportById(int id)
        {
            try
            {
                var anchor = db.CHR.FirstOrDefault(c => c.id == id);
                if (anchor == null) return NotFound();

                DateTime? batchDate = anchor.ClassDate;
                int? batchSession = anchor.sessionID;

                // Same batch = same ClassDate + same sessionID
                var data = db.CHR
                    .Where(c => c.ClassDate == batchDate && c.sessionID == batchSession)
                    .OrderBy(c => c.id)
                    .ToList()
                    .Select(c => new
                    {
                        c.id,
                        c.TeacherID,
                        c.TeacherName,
                        c.CourseCode,
                        c.Discipline,
                        c.Venue,
                        c.ClassDate,
                        c.sessionID,
                        LateIn = c.LateIn ?? 0,
                        LeftEarly = c.LeftEarly ?? 0,
                        c.Remarks,
                        Score = CalculateScore(c.LateIn, c.LeftEarly),
                        Status = GetComputedStatus(c.LateIn, c.LeftEarly, c.Status)
                    })
                    .ToList();

                return Ok(data);
            }
            catch (Exception ex)
            {
                return InternalServerError(
                    new Exception("Backend Error: " + ex.Message));
            }
        }

        // ── 3A. Filter Report ─────────────────────────────────────
        [HttpGet]
        [Route("FilterReport")]
        public IHttpActionResult FilterReport(
            int reportId,
            string courseCode = "",
            string teacherName = "",
            string classDate = "")
        {
            try
            {
                // Find batch anchor
                var anchor = db.CHR.FirstOrDefault(c => c.id == reportId);

                if (anchor == null)
                    return NotFound();

                DateTime batchDate = anchor.ClassDate;
                int? batchSession = anchor.sessionID;

                // Same batch = same date + same session
                var query = db.CHR.Where(c =>
                    c.ClassDate == batchDate &&
                    c.sessionID == batchSession);

                // ── Filter By Course ─────────────────────────
                if (!string.IsNullOrEmpty(courseCode))
                {
                    query = query.Where(c =>
                        c.CourseCode.Contains(courseCode));
                }

                // ── Filter By Teacher ───────────────────────
                if (!string.IsNullOrEmpty(teacherName))
                {
                    query = query.Where(c =>
                        c.TeacherName.Contains(teacherName));
                }

                // ── Filter By Date ──────────────────────────
                if (!string.IsNullOrEmpty(classDate))
                {
                    DateTime parsedDate;

                    if (DateTime.TryParse(classDate, out parsedDate))
                    {
                        DateTime startDate = parsedDate.Date;
                        DateTime endDate = startDate.AddDays(1);

                        query = query.Where(c =>
                            c.ClassDate >= startDate &&
                            c.ClassDate < endDate);
                    }
                }

                // ── Final Data ──────────────────────────────
                var data = query
                    .OrderBy(c => c.id)
                    .ToList()
                    .Select(c => new
                    {
                        c.id,
                        c.TeacherID,
                        c.TeacherName,
                        c.CourseCode,
                        c.Discipline,
                        c.Venue,
                        c.ClassDate,
                        c.sessionID,

                        LateIn = c.LateIn ?? 0,
                        LeftEarly = c.LeftEarly ?? 0,

                        c.Remarks,

                        Score = CalculateScore(
                            c.LateIn,
                            c.LeftEarly
                        ),

                        Status = GetComputedStatus(
                            c.LateIn,
                            c.LeftEarly,
                            c.Status
                        )
                    })
                    .ToList();

                return Ok(data);
            }
            catch (Exception ex)
            {
                return InternalServerError(
                    new Exception(
                        "Filter Error: " + ex.Message
                    )
                );
            }
        }


        // ── 4. Edit Row ──────────────────────────────────────────
        [HttpPut]
        [Route("EditRow/{id:int}")]
        public IHttpActionResult EditRow(int id, [FromBody] EditRowDto dto)
        {
            try
            {
                var row = db.CHR.FirstOrDefault(c => c.id == id);
                if (row == null) return NotFound();

                row.Remarks = dto.Remarks;
                row.LateIn = dto.LateIn;
                row.LeftEarly = dto.LeftEarly;

                db.SaveChanges();

                return Ok(new
                {
                    Message = "Updated",
                    Score = CalculateScore(row.LateIn, row.LeftEarly),
                    Status = GetComputedStatus(row.LateIn, row.LeftEarly, row.Status)
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception("Edit Error: " + ex.Message));
            }
        }

        // ── 5. Delete Row ────────────────────────────────────────
        [HttpDelete]
        [Route("DeleteRow/{id:int}")]
        public IHttpActionResult DeleteRow(int id)
        {
            try
            {
                var row = db.CHR.FirstOrDefault(c => c.id == id);
                if (row == null) return NotFound();

                db.CHR.Remove(row);
                db.SaveChanges();
                return Ok(new { Message = "Deleted", DeletedId = id });
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception("Delete Error: " + ex.Message));
            }
        }

        // ── 6. Delete Batch ──────────────────────────────────────
        [HttpDelete]
        [Route("DeleteBatch/{reportId:int}")]
        public IHttpActionResult DeleteBatch(int reportId)
        {
            try
            {
                var anchor = db.CHR.FirstOrDefault(c => c.id == reportId);
                if (anchor == null) return NotFound();

                DateTime? batchDate = anchor.ClassDate;
                int? batchSession = anchor.sessionID;

                // Same session + same date = ek batch
                var batch = db.CHR
                    .Where(c => c.ClassDate == batchDate && c.sessionID == batchSession)
                    .ToList();

                db.CHR.RemoveRange(batch);
                db.SaveChanges();
                return Ok(new { Message = "Batch Deleted", Count = batch.Count });
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception("Delete Batch Error: " + ex.Message));
            }
        }

        // ── 7. Teacher Report (Session Filter Optional) ──────────
        [HttpGet]
        [Route("GetTeacherReport")]
        public IHttpActionResult GetTeacherReport(string teacherId, int? sessionID = null)
        {
            try
            {
                if (string.IsNullOrEmpty(teacherId))
                    return BadRequest("Teacher ID required.");

                var query = db.CHR.Where(c => c.TeacherID == teacherId);
                if (sessionID.HasValue)
                    query = query.Where(c => c.sessionID == sessionID.Value);

                var data = query
                    .OrderByDescending(c => c.id)
                    .ToList()
                    .Select(c => new
                    {
                        c.id,
                        c.TeacherName,
                        c.CourseCode,
                        c.Discipline,
                        c.Venue,
                        c.ClassDate,
                        c.sessionID,
                        LateIn = c.LateIn ?? 0,
                        LeftEarly = c.LeftEarly ?? 0,
                        c.Remarks,
                        Score = CalculateScore(c.LateIn, c.LeftEarly),
                        Status = GetComputedStatus(c.LateIn, c.LeftEarly, c.Status)
                    })
                    .ToList();

                if (!data.Any()) return NotFound();
                return Ok(data);
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception("Backend Error: " + ex.Message));
            }

        }
    }
}
