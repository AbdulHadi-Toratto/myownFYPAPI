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

        //[HttpGet]
        //[Route("GetPeerWithCourses/{teacherId}")]
        //public IHttpActionResult GetPeerCourses(String teacherId)
        //{
        //    var data = db.Enrollment
        //        .Where(e => e.teacherID != teacherId)
        //        .Select(e => new
        //        {
        //            TeacherID = e.Teacher.userID,
        //            TeacherName = e.Teacher.name,
        //            CourseCode = e.courseCode
        //        })
        //        .Distinct()
        //        .ToList();

        //    return Ok(data);
        //}


        [HttpPost]
        [Route("SubmitEvaluation")]
        public IHttpActionResult SubmitEvaluation([FromBody] List<PeerEvaluation> evaluations)
        {
            if (evaluations == null || !evaluations.Any())
                return BadRequest("Invalid submission");

            // Get latest session
            var latestSession = db.Session
                                  .OrderByDescending(s => s.id) // or CreatedDate
                                  .FirstOrDefault();

            if (latestSession == null)
                return BadRequest("No active session found");

            foreach (var eval in evaluations)
            {
                var record = new PeerEvaluation
                {
                    evaluatorID = eval.evaluatorID,
                    evaluateeID = eval.evaluateeID,
                    questionID = eval.questionID,
                    courseCode = eval.courseCode,
                    score = eval.score,
                    SessionId = latestSession.id // <-- store latest session
                };

                db.PeerEvaluation.Add(record);
            }

            db.SaveChanges();

            return Ok(new { success = true, sessionID = latestSession.id });
        }



        //[HttpGet]
        //[Route("GetPeerID")]
        //public IHttpActionResult GetPeerEvaluatorID(string userId)
        //{
        //    try
        //    {
        //        // Get the PeerEvaluator entry for this teacher (you may also filter by current session)
        //        var peerEvaluator = db.PeerEvaluator
        //            .FirstOrDefault(pe => pe.teacherID.Trim().ToLower() == userId.Trim().ToLower());

        //        if (peerEvaluator == null)
        //            return Ok(new { peerEvaluatorID = (int?)null });

        //        return Ok(new { peerEvaluatorID = peerEvaluator.id });
        //    }
        //    catch (Exception ex)
        //    {
        //        return InternalServerError(ex);
        //    }
        //}

        private int GetDesignationRank(string designation)
        {
            if (string.IsNullOrWhiteSpace(designation))
                return 0;

            switch (designation.Trim().ToLower())
            {
                case "hod": return 5;                  // 🔥 highest
                case "professor": return 4;
                case "assistant professor": return 3;
                case "teacher": return 2;
                case "junior teacher": return 1;
                default: return 0;
            }
        }



        [HttpGet]
        [Route("getpeers/{userId}")]
        public IHttpActionResult GetTeachersWithCourses(string userId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(userId))
                    return BadRequest("UserId is required");

                string normalizedUserId = userId.Trim().ToLower();

                // 🔹 Current Teacher
                var currentTeacher = db.Teacher
                    .FirstOrDefault(t => t.userID.Trim().ToLower() == normalizedUserId);

                if (currentTeacher == null)
                    return Ok(new List<object>());

                int currentRank = GetDesignationRank(currentTeacher.designation);

                var data = db.Enrollment
                  .Select(e => new
                  {
                      TeacherID = e.teacherID,
                      CourseCode = e.courseCode,
                 
                      TeacherInfo = db.Teacher
                          .Where(t => t.userID == e.teacherID)
                          .Select(t => new
                          {
                              t.name,
                              t.designation
                          })
                          .FirstOrDefault()
                  })
                  .ToList()
                 
                  // 🔥 FILTER LOGIC
                  .Where(t =>
                  {
                      if (t.TeacherInfo == null)
                          return false;
                 
                      int targetRank = GetDesignationRank(t.TeacherInfo.designation);
                 
                      // ❌ no self evaluation
                      if (t.TeacherID.Trim().ToLower() == normalizedUserId)
                          return false;
                 
                      // 🔥 same level + lower
                      return targetRank <= currentRank;
                  })
                 
                  // 🔥 FINAL SHAPE
                  .Select(t => new
                  {
                      TeacherID = t.TeacherID,
                      TeacherName = t.TeacherInfo.name,
                      CourseCode = t.CourseCode
                  })
                  .Distinct() // optional (avoid duplicates)
                  .ToList();

                return Ok(data);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        [HttpGet]
        [Route("GetSubmittedEvaluations")]
        public IHttpActionResult GetSubmittedEvaluations(int evaluatorID)
        {
            // fetch all submitted evaluations for this evaluator
            var submitted = db.PeerEvaluation
                .Where(p => p.evaluatorID == evaluatorID)
                .Select(p => new
                {
                    TeacherID = p.evaluateeID, // if your evaluateeID is int, adjust type
                    CourseCode = p.courseCode
                })
                .Distinct() // one entry per course per teacher
                .ToList();

            return Ok(submitted);
        }

        [HttpGet]
        [Route("GetPeerEvaluatorID")]
        public IHttpActionResult GetPeerEvaluatorID(string userId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(userId))
                    return BadRequest("UserId is required");

                string normalizedUserId = userId.Trim().ToLower();

                var teacher = db.Teacher
                    .FirstOrDefault(t => t.userID.Trim().ToLower() == normalizedUserId);

                if (teacher == null)
                    return Ok(new { peerEvaluatorID = (int?)null, isAllowed = false });

                var latestSession = db.Session
                    .OrderByDescending(s => s.id)
                    .FirstOrDefault();

                if (latestSession == null)
                    return Ok(new { peerEvaluatorID = (int?)null, isAllowed = false });

                bool isPermanent = teacher.isPermanentEvaluator == 1;

                // STEP 1: check existing evaluator in latest session
                var peerEvaluator = db.PeerEvaluator
                    .FirstOrDefault(pe =>
                        pe.teacherID.Trim().ToLower() == normalizedUserId &&
                        pe.sessionID == latestSession.id
                    );

                // STEP 2: AUTO INSERT ONLY ONCE (FIXED)
                if (isPermanent && peerEvaluator == null)
                {
                    peerEvaluator = new PeerEvaluator
                    {
                        teacherID = normalizedUserId,
                        sessionID = latestSession.id
                    };

                    db.PeerEvaluator.Add(peerEvaluator);
                    db.SaveChanges(); // save immediately so ID is generated
                }

                // STEP 3: response
                if (peerEvaluator != null)
                {
                    return Ok(new
                    {
                        peerEvaluatorID = peerEvaluator.id,
                        isAllowed = true,
                        source = isPermanent ? "PermanentTeacherAutoAdded" : "SessionEvaluator"
                    });
                }

                return Ok(new
                {
                    peerEvaluatorID = (int?)null,
                    isAllowed = false,
                    source = "NotEvaluator"
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }




    }
}
