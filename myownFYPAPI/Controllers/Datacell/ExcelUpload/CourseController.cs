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
    [RoutePrefix("api/uploadCourse")]
    public class CourseController : ApiController
    {
        fypapiv1Entities db = new fypapiv1Entities();

        [HttpPost]
        [Route("upload")]
        public IHttpActionResult UploadCourse()
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

                    foreach (DataRow row in dataTable.Rows)
                    {
                        if (row["Code"] == DBNull.Value || row["Title"] == DBNull.Value) continue;

                        string code = row["Code"].ToString().Trim();
                        string title = row["Title"].ToString().Trim();

                        if (db.Course.Find(code) != null) continue;

                        db.Course.Add(new myownFYPAPI.Models.Course { code = code, title = title });
                        insertedCount++;
                    }

                    db.SaveChanges();
                    return Ok($"{insertedCount} courses uploaded successfully.");
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }
    }
}