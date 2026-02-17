using myownFYPAPI.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Routing;
using System.Web.Http;
using myownFYPAPI.Models.DTO;

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
            var result = db.Enrollment
                .Where(e => e.studentID == studentId)
                .Select(e => new StudentEnrollementDto
                {
                    EnrollmentID = e.id,
                    CourseCode = e.Course.code,
                    CourseTitle = e.Course.title,
                    TeacherName = e.Teacher.name,
                    SessionName = e.Session.name
                })
                .ToList();

            if (result.Count == 0)
                return NotFound();

            return Ok(result);
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

    }
}