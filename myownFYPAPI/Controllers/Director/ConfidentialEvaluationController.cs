using myownFYPAPI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace myownFYPAPI.Controllers.Director
{
    [RoutePrefix("api/director/confidential-evaluation")]
    public class ConfidentialEvaluationController : ApiController
    {

        fypapiv1Entities db = new fypapiv1Entities();


        [HttpPost]
        [Route("get-evaluations")]
        public IHttpActionResult GetEvaluations([FromBody] Email request)
        {
            try
            {
                var emailRecord = db.Email
                    .FirstOrDefault(e => e.mail == request.mail);

                if (emailRecord == null)
                    return BadRequest("Email not found");

                var evaluations = new List<object>();

                using (var client = new MailKit.Net.Imap.ImapClient())
                {
                    client.Connect("imap.gmail.com", 993, true);
                    client.Authenticate(emailRecord.mail, emailRecord.password);

                    var inbox = client.Inbox;
                    inbox.Open(MailKit.FolderAccess.ReadOnly);

                    var query = MailKit.Search.SearchQuery
                        .SubjectContains("Confidential Evaluation");

                    var uids = inbox.Search(query);





                    foreach (var uid in uids.Reverse())
                    {
                        var message = inbox.GetMessage(uid);

                        var body = message.TextBody ?? message.HtmlBody;

                        if (string.IsNullOrEmpty(body))
                            continue;

                        var start = body.IndexOf("START_EVAL");
                        var end = body.IndexOf("END_EVAL");



                        if (start == -1 || end == -1 || end <= start)
                            continue;

                        try
                        {
                            var json = body.Substring(start + 11, end - (start + 11)).Trim();

                            var parsed = Newtonsoft.Json.Linq.JObject.Parse(json);

                            string studentId = (string)parsed["studentId"];
                            string teacherId = (string)parsed["teacherId"];

                            var student = db.Student.FirstOrDefault(s => s.userID.ToString() == studentId);
                            var teacher = db.Teacher.FirstOrDefault(t => t.userID.ToString() == teacherId);

                            evaluations.Add(new
                            {
                                studentId,
                                studentName = student?.name,

                                teacherId,
                                teacherName = teacher?.name,

                                session = (string)parsed["session"],
                                subjectCode = (string)parsed["subjectCode"],
                                submittedOn = (DateTime)parsed["submittedOn"],

                                evaluation = parsed["evaluation"]
                            });
                        }


                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine("PARSE ERROR: " + ex.Message);
                        }



                    }

                    client.Disconnect(true);
                }



                return Ok(new
                {
                    success = true,
                    count = evaluations.Count,
                    data = evaluations,



                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    error = ex.Message

                });
            }
        }



    }
}
