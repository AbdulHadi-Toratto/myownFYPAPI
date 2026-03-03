using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using myownFYPAPI.Models;

namespace myownFYPAPI.Controllers.Teacher
{
        [RoutePrefix("api/TeacherDashboard")]
    public class TeacherDashboardController : ApiController
    {
        fypapiv1Entities db = new fypapiv1Entities();


        [HttpGet]
        [Route("GetTeacherName/{teacherId}")]
        public IHttpActionResult GetTeacherName(string teacherId)
        {
            if (string.IsNullOrEmpty(teacherId))
                return BadRequest("Teacher ID is required.");

            var teacher = db.Teacher
                            .Where(t => t.userID == teacherId)
                            .Select(t => t.name)
                            .FirstOrDefault();

            if (teacher == null)
                return NotFound();

            return Ok(teacher);
        }

        [HttpGet]
        [Route("GetPeerWithCourses/{teacherId}")]
        public IHttpActionResult GetPeerCourses(String teacherId)
        {
            var data = db.Enrollment
                .Where(e => e.teacherID != teacherId)
                .Select(e => new
                {
                    TeacherID = e.Teacher.userID,
                    TeacherName = e.Teacher.name,
                    CourseCode = e.courseCode
                })
                .Distinct()
                .ToList();

            return Ok(data);
        }


        [HttpPost]
        [Route("SubmitPeerEvaluation")]
        public IHttpActionResult SubmitEvaluation([FromBody] List<PeerEvaluation> evaluations)
        {
            if (evaluations == null || !evaluations.Any())
                return BadRequest("Invalid submission");

            foreach (var eval in evaluations)
            {
                var record = new PeerEvaluation
                {
                    evaluatorID = eval.evaluatorID,
                    evaluateeID = eval.evaluateeID,
                    questionID = eval.questionID,
                    courseCode = eval.courseCode,
                    score = eval.score
                };

                db.PeerEvaluation.Add(record);
            }

            db.SaveChanges();

            return Ok(new { success = true });
        }


        [HttpGet]
        [Route("GetPeerID")]
        public IHttpActionResult GetPeerEvaluatorID(string userId)
        {
            try
            {
                // Get the PeerEvaluator entry for this teacher (you may also filter by current session)
                var peerEvaluator = db.PeerEvaluator
                    .FirstOrDefault(pe => pe.teacherID.Trim().ToLower() == userId.Trim().ToLower());

                if (peerEvaluator == null)
                    return Ok(new { peerEvaluatorID = (int?)null });

                return Ok(new { peerEvaluatorID = peerEvaluator.id });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }


    }
}
