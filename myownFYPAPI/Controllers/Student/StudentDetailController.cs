using myownFYPAPI.Models;
using myownFYPAPI.Models.DTO;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Web.Http;
using System.Web.Routing;

namespace myownFYPAPI.Controllers.Student
{
    [RoutePrefix("api/Student")]

    public class StudentDetailController : ApiController
    {
        fypapiv1Entities db = new fypapiv1Entities();

        [HttpGet]
        [Route("GetStudentEnrollments/{studentId}")]
        public IHttpActionResult GetStudentEnrollments(string studentId)
        {
            
            var latestSession = db.Session
                .OrderByDescending(s => s.id)
                .FirstOrDefault();

            if (latestSession == null)
                return NotFound();

           
            var enrollments = db.Enrollment
                .Where(e => e.studentID == studentId && e.sessionID == latestSession.id)
                .Select(e => new
                {
                    EnrollmentID = e.id,
                    CourseCode = e.courseCode,
                    CourseTitle = e.Course.title,

                    TeacherID = e.teacherID,
                    TeacherName = e.Teacher.name,

                    SessionID = e.sessionID,
                    SessionName = e.Session.name
                })
                .ToList();

            if (!enrollments.Any())
                return NotFound();

            return Ok(enrollments);
        }
        

        [HttpPost]
        [Route("SubmitStudentEvaluation")]
        public IHttpActionResult SubmitStudentEvaluation(
  [FromBody] List<StudentEvaluation> evaluations)
        {
            if (evaluations == null || !evaluations.Any())
                return BadRequest("Invalid submission");
            try
            {
                // ✅ Get latest session from DB
                var latestSession = db.Session
                    .OrderByDescending(s => s.id)
                    .FirstOrDefault();

                if (latestSession == null)
                    return BadRequest("No active session found");

                foreach (var e in evaluations)
                {
                    db.StudentEvaluation.Add(new StudentEvaluation
                    {
                        enrollmentID = e.enrollmentID,
                        questionID = e.questionID,
                        score = e.score,
                        StudentId = e.StudentId,
                        SessionID = latestSession.id   // ✅ FIXED HERE
                    });
                }

                db.SaveChanges();

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    error = ex.Message
                });
            }
         return Ok(new { success = true });
        }


        [HttpGet]
        [Route("GetStudentName/{studentId}")]
        public IHttpActionResult GetStudentName(string studentId)
        {
            var student = db.Student.FirstOrDefault(s => s.userID == studentId);
            if (student == null)
                return NotFound();
            return Ok(student.name);
        }

        [HttpGet]
        [Route("GetActiveQuestionnaire/{type}")]
        public IHttpActionResult GetActiveQuestionnaire(string type)
        {
            try
            {
                // Get Questionnaire where flag = '1'
                var questionnaire = db.Questionare
                    .Include("Questions")
                    .Where(q => q.flag == "1" && q.type == type )
                    .Select(q => new
                    {
                        QuestionareID = q.id,
                        Type = q.type,
                        Flag = q.flag,
                        Questions = q.Questions.Select(ques => new
                        {
                            ques.QuestionID,
                            ques.QuestionText
                        }).ToList()
                    })
                    .FirstOrDefault();

                if (questionnaire == null)
                    return Ok(new { Message = "No active questionnaire found" });

                return Ok(questionnaire);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }


        [HttpGet]
        [Route("GetSubmittedStudentEvaluations/{studentId}")]
        public IHttpActionResult GetSubmittedStudentEvaluations(string studentId)
        {
            var latestSession = db.Session
               .OrderByDescending(s => s.id)
               .FirstOrDefault();

            if (latestSession == null)
                return Ok(new List<int>());


            var submitted = db.StudentEvaluation
                .Where(se => se.StudentId.Trim().ToLower() == studentId.Trim().ToLower() && se.SessionID == latestSession.id)
                .Select(se => se.enrollmentID)
                .Distinct()
                .ToList();

            return Ok(submitted);
        }
        [HttpPost]
        [Route("SubmitConfidentialEvaluation")]
        public IHttpActionResult SubmitConfidentialEvaluation(
            [FromBody] ConfidentialEvaluationDto model)
        {
            if (model == null || model.Answers == null || !model.Answers.Any())
                return BadRequest("Invalid submission");

            try
            {
                
                var enrollment = db.Enrollment
                    .FirstOrDefault(e => e.id == model.EnrollmentId);

                if (enrollment == null)
                    return NotFound();

                
                var student = db.Student
                    .FirstOrDefault(s => s.userID == model.StudentId);

                var teacher = db.Teacher
                    .FirstOrDefault(t => t.userID == enrollment.teacherID);

                var course = db.Course
                    .FirstOrDefault(c => c.code == enrollment.courseCode);

                var questionIds = model.Answers.Select(a => a.questionId).ToList();

                var questions = db.Questions
                    .Where(q => questionIds.Contains(q.QuestionID))
                    .ToList();

                // 🔹 Build Email Body

                //string body = "";
                //body += "CONFIDENTIAL EVALUATION\n\n";
                //body += "Student: " + student?.name + "\n";
                //body += "Teacher: " + teacher?.name + "\n";
                //body += "Course: " + course?.title + "\n";
                //body += "Date: " + DateTime.Now + "\n\n";
                //body += "-----------------------------\n";
                //body += "Questions & Answers\n";
                //body += "-----------------------------\n\n";

                //foreach (var ans in model.Answers)
                //{
                //    var question = questions
                //        .FirstOrDefault(q => q.QuestionID == ans.questionId);

                //    body += "Q: " + question?.QuestionText + "\n";
                //    body += "Score: " + ans.score + "\n\n";
                //}

                var emailObject = new
                {
                    studentId = model.StudentId,
                    teacherId = teacher?.userID,
                    session = enrollment.Session.name,
                    subjectCode = enrollment.courseCode,
                    submittedOn = DateTime.Now,
                    evaluation = model.Answers.Select(a =>
                    {
                        var question = questions
                            .FirstOrDefault(q => q.QuestionID == a.questionId);

                        return new
                        {
                            qId = a.questionId,
                            questionText = question?.QuestionText,
                            score = a.score
                        };
                    }).ToList()
                };

                string body = Newtonsoft.Json.JsonConvert.SerializeObject(emailObject, Newtonsoft.Json.Formatting.Indented);



                SendEmail(body);

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    error = ex.Message,
                    inner = ex.InnerException?.Message
                });
            }
        }


      
            private  void SendEmail(string body)
            {
                var fromAddress = new MailAddress("biit.epas.system@gmail.com");

                var Active_Email = db.Email.FirstOrDefault(x => x.isActive == true);
            var active_adress = Active_Email.mail;
            var toAddress = new MailAddress(active_adress);

                const string fromPassword = "viylzrgalznlcnys";

                var smtp = new SmtpClient
                {
                    Host = "smtp.gmail.com",
                    Port = 587,
                    EnableSsl = true,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(
                        fromAddress.Address,
                        fromPassword)
                };

                using (var message = new MailMessage(fromAddress, toAddress)
                {
                    Subject = "Confidential Evaluation - EPAS",
                    Body = body
                })
                {
                    smtp.Send(message);
                }
            }
        
    }
}