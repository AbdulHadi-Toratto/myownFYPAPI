using ExcelDataReader;
using myownFYPAPI.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Http;
using static System.Net.WebRequestMethods;


namespace myownFYPAPI.Controllers.Datacell.ExcelUpload
{
    [RoutePrefix("api/uploadEnrollement")]
    public class EnrollementController : ApiController
    {
        fypapiv1Entities db = new fypapiv1Entities();

        [HttpGet]
        [Route("GetAllSessions")]
        public IHttpActionResult GetSessions()
        {
            var sessions = db.Session
                .Select(s => new
                {
                    s.id,
                    s.name
                })
                .ToList();

            return Ok(sessions);
        }





        [HttpPost]
        [Route("UploadEnrollment")]
        public IHttpActionResult UploadEnrollment()
        {
            try
            {
                var httpRequest = HttpContext.Current.Request;

                if (string.IsNullOrEmpty(httpRequest.Form["sessionId"]))
                    return BadRequest("Session not selected.");

                int sessionId = int.Parse(httpRequest.Form["sessionId"]);

                if (db.Session.Find(sessionId) == null)
                    return BadRequest("Invalid session.");

                if (httpRequest.Files.Count == 0)
                    return BadRequest("No file uploaded.");

                var file = httpRequest.Files[0];
                if (file == null || file.ContentLength == 0)
                    return BadRequest("Empty file.");

                System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

                int inserted = 0;
                int updated = 0;

                using (var stream = file.InputStream)
                using (var reader = ExcelReaderFactory.CreateReader(stream))
                {
                    var result = reader.AsDataSet(new ExcelDataSetConfiguration()
                    {
                        ConfigureDataTable = (_) => new ExcelDataTableConfiguration()
                        {
                            UseHeaderRow = true
                        }
                    });

                    var table = result.Tables[0];

                    // ✅ Column loop se bahar — ek baar dhundo
                    var sectionCol = table.Columns.Cast<DataColumn>()
                        .FirstOrDefault(c => c.ColumnName.Equals("Section", StringComparison.OrdinalIgnoreCase));
                    var gradeCol = table.Columns.Cast<DataColumn>()
                        .FirstOrDefault(c => c.ColumnName.Equals("Grade", StringComparison.OrdinalIgnoreCase));

                    foreach (DataRow row in table.Rows)
                    {
                        string studentID = row["studentID"]?.ToString().Trim();
                        string teacherID = row["teacherID"]?.ToString().Trim();
                        string courseCode = row["courseCode"]?.ToString().Trim();

                        string section = (sectionCol != null && row[sectionCol] != DBNull.Value)
                            ? row[sectionCol].ToString().Trim() : null;

                        string grade = (gradeCol != null && row[gradeCol] != DBNull.Value)
                            ? row[gradeCol].ToString().Trim() : null;

                        if (string.IsNullOrEmpty(studentID) ||
                            string.IsNullOrEmpty(teacherID) ||
                            string.IsNullOrEmpty(courseCode))
                            continue;

                        var existingEnrollment = db.Enrollment.FirstOrDefault(e =>
                            e.studentID == studentID &&
                            e.courseCode == courseCode &&
                            e.sessionID == sessionId
                        );

                        if (existingEnrollment != null)
                        {
                            // ✅ Update Section aur Grade
                            existingEnrollment.teacherID = teacherID; // TeacherID bhi update karna chahte hain
                            existingEnrollment.courseCode = courseCode; // CourseCode bhi update karna chahte hain
                            existingEnrollment.Section = section;
                            existingEnrollment.Grade = grade;
                            updated++;
                            continue;
                        }

                        // ✅ Naya record insert karo
                        db.Enrollment.Add(new Enrollment
                        {
                            studentID = studentID,
                            teacherID = teacherID,
                            courseCode = courseCode,
                            sessionID = sessionId,
                            Section = section,
                            Grade = grade
                        });

                        inserted++;
                    }

                    db.SaveChanges();
                }

                return Ok($"{inserted} new enrollments added. {updated} existing records updated (Section & Grade).");
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }
    }


    //[HttpPost]
    //[Route("UploadEnrollment")]
    //public IHttpActionResult UploadEnrollment()
    //{
    //    try
    //    {
    //        var httpRequest = HttpContext.Current.Request;

    //        // ✅ read sessionId from frontend
    //        int sessionId;
    //        if (!int.TryParse(httpRequest["sessionId"], out sessionId))
    //            return BadRequest("SessionID missing or invalid");

    //        if (httpRequest.Files.Count == 0)
    //            return BadRequest("No file uploaded.");

    //        var file = httpRequest.Files[0];

    //        System.Text.Encoding.RegisterProvider(
    //            System.Text.CodePagesEncodingProvider.Instance);

    //        using (var stream = file.InputStream)
    //        using (var reader = ExcelReaderFactory.CreateReader(stream))
    //        {
    //            var result = reader.AsDataSet(new ExcelDataSetConfiguration()
    //            {
    //                ConfigureDataTable = (_) =>
    //                    new ExcelDataTableConfiguration() { UseHeaderRow = true }
    //            });

    //            var table = result.Tables[0];

    //            int inserted = 0;
    //            int skippedInvalidFK = 0;
    //            int skippedDuplicate = 0;

    //            foreach (DataRow row in table.Rows)
    //            {
    //                if (row["StudentID"] == DBNull.Value ||
    //                    row["TeacherID"] == DBNull.Value ||
    //                    row["CourseCode"] == DBNull.Value)
    //                    continue;

    //                string studentId = row["StudentID"].ToString().Trim();
    //                string teacherId = row["TeacherID"].ToString().Trim();
    //                string courseCode = row["CourseCode"].ToString().Trim();

    //                // ✅ FK validation
    //                if (db.Student.Find(studentId) == null ||
    //                    db.Teacher.Find(teacherId) == null ||
    //                    db.Course.Find(courseCode) == null ||
    //                    db.Session.Find(sessionId) == null)
    //                {
    //                    skippedInvalidFK++;
    //                    continue;
    //                }

    //                // ✅ duplicate check
    //                bool exists = db.Enrollment.Any(e =>
    //                    e.studentID == studentId &&
    //                    e.teacherID == teacherId &&
    //                    e.courseCode == courseCode &&
    //                    e.sessionID == sessionId);

    //                if (exists)
    //                {
    //                    skippedDuplicate++;
    //                    continue;
    //                }

    //                db.Enrollment.Add(new myownFYPAPI.Models.Enrollment
    //                {
    //                    studentID = studentId,
    //                    teacherID = teacherId,
    //                    courseCode = courseCode,
    //                    sessionID = sessionId   // ✅ from dropdown
    //                });

    //                inserted++;
    //            }

    //            db.SaveChanges();

    //            return Ok($"{inserted} enrollments added. " +
    //                      $"{skippedDuplicate} duplicates skipped. " +
    //                      $"{skippedInvalidFK} invalid FK rows skipped.");
    //        }
    //    }
    //    catch (Exception ex)
    //    {
    //        return Content(System.Net.HttpStatusCode.InternalServerError, ex.ToString());
    //    }
    //}

}

