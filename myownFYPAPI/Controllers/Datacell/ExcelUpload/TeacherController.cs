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
using TeacherModel = myownFYPAPI.Models.Teacher;

namespace myownFYPAPI.Controllers.Datacell.ExcelUpload
{
    [RoutePrefix("api/uploadTeacher")]
    public class TeacherController : ApiController
    {
        fypapiv1Entities db = new fypapiv1Entities();
        //[HttpPost]
        //[Route("upload")]
        //public IHttpActionResult Upload()
        //{
        //    try
        //    {
        //        var httpRequest = HttpContext.Current.Request;

        //        if (httpRequest.Files.Count == 0)
        //            return BadRequest("No file uploaded.");

        //        var file = httpRequest.Files[0];

        //        if (file == null || file.ContentLength == 0)
        //            return BadRequest("Empty file.");

        //        // Needed for ExcelDataReader
        //        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        //        using (var stream = file.InputStream)
        //        {
        //            using (var reader = ExcelReaderFactory.CreateReader(stream))
        //            {
        //                var result = reader.AsDataSet(new ExcelDataSetConfiguration()
        //                {
        //                    ConfigureDataTable = (_) => new ExcelDataTableConfiguration()
        //                    {
        //                        UseHeaderRow = true
        //                    }
        //                });

        //                var dataTable = result.Tables[0];

        //                foreach (DataRow row in dataTable.Rows)
        //                {
        //                    string userId = row["Userid"].ToString();

        //                    // 1️⃣ Check if the user already exists
        //                    var existingUser = db.Users.Find(userId);

        //                    if (existingUser == null)
        //                    {
        //                        // 2️⃣ User doesn't exist, add to Users first
        //                        Users newUser = new Users
        //                        {
        //                            id = userId,
        //                            password = "default123",       // You can customize this
        //                            role = "Teacher",
        //                            profileImagePath = null,
        //                            isActive = 1
        //                        };
        //                        db.Users.Add(newUser);
        //                    }

        //                    // 3️⃣ Add to Teacher table
        //                    TeacherModel teacher = new TeacherModel()
        //                    {
        //                        userID = userId,
        //                        name = row["Name"].ToString(),
        //                        department = row["Department"].ToString()
        //                    };

        //                    db.Teacher.Add(teacher);
        //                }

        //                // Save all changes (Users + Teacher) in one transaction
        //                db.SaveChanges();
        //            }
        //        }

        //        return Ok("File uploaded and data saved successfully.");
        //    }
        //    catch (Exception ex)
        //    {
        //        return BadRequest(ex.Message);
        //    }
        //}


        [HttpPost]
        [Route("upload")]
        public IHttpActionResult Upload()
        {
            try
            {
                var httpRequest = HttpContext.Current.Request;
                if (httpRequest.Files.Count == 0) return BadRequest("No file uploaded.");

                var file = httpRequest.Files[0];

                // System.Text Encoding registration for ExcelDataReader
                System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

                int insertedCount = 0;
                int updatedCount = 0;

                using (var stream = file.InputStream)
                {
                    // ✅ Reader ko stream ke andar hi hona chahiye
                    using (var reader = ExcelReaderFactory.CreateReader(stream))
                    {
                        var result = reader.AsDataSet(new ExcelDataSetConfiguration()
                        {
                            ConfigureDataTable = (_) => new ExcelDataTableConfiguration() { UseHeaderRow = true }
                        });

                        var dataTable = result.Tables[0];

                        foreach (DataRow row in dataTable.Rows)
                        {
                            // ✅ Null check with case-sensitive headers
                            if (row["UserID"] == DBNull.Value || row["Name"] == DBNull.Value ||
                                row["Department"] == DBNull.Value || row["Designation"] == DBNull.Value)
                            {
                                continue;
                            }

                            string userId = row["UserID"].ToString().Trim();
                            string name = row["Name"].ToString().Trim();
                            string department = row["Department"].ToString().Trim();
                            string designation = row["Designation"].ToString().Trim();

                            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(name)) continue;

                            // 1. Handle Users Table (Login Account)
                            var existingUser = db.Users.Find(userId);
                            if (existingUser == null)
                            {
                                db.Users.Add(new myownFYPAPI.Models.Users
                                {
                                    id = userId,
                                    password = "default123", // Default password
                                    role = "Teacher",
                                    isActive = 1
                                });
                            }

                            // 2. Handle Teacher Table (Add or Update Logic)
                            var teacher = db.Teacher.Find(userId);
                            if (teacher == null)
                            {
                                // Naya Teacher Add karein
                                db.Teacher.Add(new myownFYPAPI.Models.Teacher
                                {
                                    userID = userId,
                                    name = name,
                                    department = department,
                                    designation = designation,
                                    isPermanentEvaluator = 0
                                });
                                insertedCount++;
                            }
                            else
                            {
                                // Pehle se mojood teacher ko update karein
                                teacher.name = name;
                                teacher.department = department;
                                teacher.designation = designation;
                                updatedCount++;
                            }
                        }

                        db.SaveChanges();
                    }
                }

                // ✅ Final Message logic for Alert
                string finalMessage = "";
                if (insertedCount > 0 && updatedCount > 0)
                {
                    finalMessage = $"{insertedCount} Teachers Added & {updatedCount} Updated Successfully!";
                }
                else if (insertedCount > 0)
                {
                    finalMessage = $"{insertedCount} New Teachers Added Successfully!";
                }
                else if (updatedCount > 0)
                {
                    finalMessage = $"{updatedCount} Teachers Updated Successfully!";
                }
                else
                {
                    finalMessage = "Excel Processed: No new changes found.";
                }

                // Return as simple string for React alert
                return Ok(finalMessage);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }


        [HttpGet]
        [Route("GetTeachers")]
        public HttpResponseMessage GetTeachers()
        {
            var res = db.Teacher.ToList();

            if (res.Count == 0)
            {
                return Request.CreateResponse(HttpStatusCode.NotFound, "No Teacher Found");
            }

            return Request.CreateResponse(HttpStatusCode.OK, res);

        }
    }
}