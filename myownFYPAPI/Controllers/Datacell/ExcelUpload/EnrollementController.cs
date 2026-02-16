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

                // ✅ read sessionId from frontend
                int sessionId;
                if (!int.TryParse(httpRequest["sessionId"], out sessionId))
                    return BadRequest("SessionID missing or invalid");

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
                        ConfigureDataTable = (_) =>
                            new ExcelDataTableConfiguration() { UseHeaderRow = true }
                    });

                    var table = result.Tables[0];

                    int inserted = 0;
                    int skippedInvalidFK = 0;
                    int skippedDuplicate = 0;

                    foreach (DataRow row in table.Rows)
                    {
                        if (row["StudentID"] == DBNull.Value ||
                            row["TeacherID"] == DBNull.Value ||
                            row["CourseCode"] == DBNull.Value)
                            continue;

                        string studentId = row["StudentID"].ToString().Trim();
                        string teacherId = row["TeacherID"].ToString().Trim();
                        string courseCode = row["CourseCode"].ToString().Trim();

                        // ✅ FK validation
                        if (db.Student.Find(studentId) == null ||
                            db.Teacher.Find(teacherId) == null ||
                            db.Course.Find(courseCode) == null ||
                            db.Session.Find(sessionId) == null)
                        {
                            skippedInvalidFK++;
                            continue;
                        }

                        // ✅ duplicate check
                        bool exists = db.Enrollment.Any(e =>
                            e.studentID == studentId &&
                            e.teacherID == teacherId &&
                            e.courseCode == courseCode &&
                            e.sessionID == sessionId);

                        if (exists)
                        {
                            skippedDuplicate++;
                            continue;
                        }

                        db.Enrollment.Add(new myownFYPAPI.Models.Enrollment
                        {
                            studentID = studentId,
                            teacherID = teacherId,
                            courseCode = courseCode,
                            sessionID = sessionId   // ✅ from dropdown
                        });

                        inserted++;
                    }

                    db.SaveChanges();

                    return Ok($"{inserted} enrollments added. " +
                              $"{skippedDuplicate} duplicates skipped. " +
                              $"{skippedInvalidFK} invalid FK rows skipped.");
                }
            }
            catch (Exception ex)
            {
                return Content(System.Net.HttpStatusCode.InternalServerError, ex.ToString());
            }
        }

    }
}
