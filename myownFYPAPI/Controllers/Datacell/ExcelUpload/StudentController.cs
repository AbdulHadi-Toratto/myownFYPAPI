using ExcelDataReader;
using myownFYPAPI.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Web;
using System.Web.Http;
using static System.Net.WebRequestMethods;

namespace myownFYPAPI.Controllers.Datacell.ExcelUpload
{
    [RoutePrefix("api/uploadStudent")]
    public class StudentController : ApiController
    {
        fypapiv1Entities db = new fypapiv1Entities();

        [HttpPost]
        [Route("upload")]
        public IHttpActionResult UploadStudent()
        {
            try
            {
                var httpRequest = HttpContext.Current.Request;

                if (httpRequest.Files.Count == 0)
                    return BadRequest("No file uploaded.");

                var file = httpRequest.Files[0];
                if (file == null || file.ContentLength == 0)
                    return BadRequest("Empty file.");


                System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

                using (var stream = file.InputStream)
                using (var reader = ExcelReaderFactory.CreateReader(stream))
                {
                    var result = reader.AsDataSet(new ExcelDataSetConfiguration()
                    {
                        ConfigureDataTable = (_) => new ExcelDataTableConfiguration() { UseHeaderRow = true }
                    });

                    var dataTable = result.Tables[0];

                    int insertedCount = 0;
                    int skippedInvalidSession = 0;
                    int skippedDuplicate = 0;

                    foreach (DataRow row in dataTable.Rows)
                    {

                        if (row["UserID"] == DBNull.Value || row["Name"] == DBNull.Value || row["AdmissionSessionId"] == DBNull.Value)
                            continue;

                        string userId = row["UserID"].ToString().Trim();
                        string name = row["Name"].ToString().Trim();
                        int sessionId;

                        // Try parse AdmissionSessionId
                        if (!int.TryParse(row["AdmissionSessionId"].ToString(), out sessionId))
                            continue;

                        // Check if session exists
                        if (db.Session.Find(sessionId) == null)
                        {
                            skippedInvalidSession++;
                            continue;
                        }

                        // 1️⃣ Check if user exists in Users table
                        var existingUser = db.Users.Find(userId);

                        if (existingUser == null)
                        {
                            // 2️⃣ User doesn't exist, add to Users first
                            Users newUser = new Users
                            {
                                id = userId,
                                password = "default123",       // Can be customized
                                role = "Student",
                                profileImagePath = null,
                                isActive = 1
                            };
                            db.Users.Add(newUser);
                        }

                        // 3️⃣ Check if student already exists in Student table
                        if (db.Student.Find(userId) != null)
                        {
                            skippedDuplicate++;
                            continue;
                        }

                        // 4️⃣ Add to Student table
                        db.Student.Add(new myownFYPAPI.Models.Student
                        {
                            userID = userId,
                            name = name,
                            admissionSessionID = sessionId
                        });

                        insertedCount++;
                    }

                    // Save all changes in one transaction (Users + Student)
                    db.SaveChanges();

                    string message = $"{insertedCount} students uploaded successfully.";
                    if (skippedDuplicate > 0) message += $" {skippedDuplicate} duplicate(s) skipped.";
                    if (skippedInvalidSession > 0) message += $" {skippedInvalidSession} row(s) skipped due to invalid session ID.";

                    return Ok(message);
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }
        [HttpGet]
        [Route("ping")]
        public IHttpActionResult PingStudent() => Ok("Student API is alive");
    }
}