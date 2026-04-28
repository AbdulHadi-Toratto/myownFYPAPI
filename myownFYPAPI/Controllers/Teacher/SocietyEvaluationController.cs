using myownFYPAPI.Models;
using myownFYPAPI.Models.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace myownFYPAPI.Controllers.Teacher
{
    [RoutePrefix("api/SocietyEvaluation")]
    public class SocietyEvaluationController : ApiController
    {
        fypapiv1Entities db = new fypapiv1Entities();


        [HttpPost]
        [Route("Submit")]
        public IHttpActionResult SubmitSocietyEvaluation([FromBody] List<SocietyEvaluationDTO> evaluations)
        {
            if (evaluations == null || !evaluations.Any())
                return BadRequest("Invalid submission");

            try
            {
                var latestSession = db.Session
                    .OrderByDescending(s => s.id)
                    .FirstOrDefault();

                if (latestSession == null)
                    return BadRequest("No active session");

                foreach (var e in evaluations)
                {
                    // 🔒 Prevent duplicate (VERY IMPORTANT)
                    var exists = db.SocietyEvaluation.Any(x =>
                        x.EvaluatorId == e.EvaluatorId &&
                        x.EvaluateeId == e.EvaluateeId &&
                        x.SocietyId == e.SocietyId &&
                        x.QuestionId == e.QuestionId &&
                        x.SessionId == latestSession.id &&
                        x.EvaluationType.Trim().ToLower() == e.EvaluationType.Trim().ToLower()
                    );

                    if (!exists)
                    {
                        db.SocietyEvaluation.Add(new SocietyEvaluation
                        {
                            EvaluatorId = e.EvaluatorId,
                            EvaluateeId = e.EvaluateeId,
                            SocietyId = e.SocietyId,
                            QuestionId = e.QuestionId,
                            Score = e.Score,
                            SessionId = latestSession.id,
                            EvaluationType = e.EvaluationType
                        });
                    }
                }

                db.SaveChanges();

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, error = ex.Message });
            }
        }

        [HttpGet]
        [Route("GetSubmitted/{evaluatorId}/{evaluationType}/{sessionId}")]
        public IHttpActionResult GetSubmittedEvaluations(string evaluatorId, string evaluationType , int sessionId)
        {

            var submitted = db.SocietyEvaluation
                .Where(x =>
                    x.EvaluatorId.Trim().ToLower() == evaluatorId.Trim().ToLower() &&
                    x.SessionId == sessionId &&
                    x.EvaluationType == evaluationType
                )
                .Select(x => x.EvaluateeId)
                .Distinct()
                .ToList();

            return Ok(submitted);
        }

        [HttpGet]
        [Route("GetChairpersonSocietyWithMentors/{teacherId}/{sessionId}")]
        public IHttpActionResult GetChairpersonSocietyWithMentors(string teacherId, int sessionId)
        {
            // 🔹 Find society where this teacher is chairperson
            var society = db.SocietyAssignments
                .Where(x => x.TeacherId == teacherId &&
                            x.SessionId == sessionId &&
                            x.IsChairperson == true)
                .Select(x => new
                {
                    x.SocietyId,
                    SocietyName = x.Societies.SocietyName
                })
                .FirstOrDefault();

            // ❌ Not a chairperson
            if (society == null)
            {
                return Ok(new
                {
                    IsChairperson = false
                });
            }

            // 🔹 Get mentors of that society
            var mentors = db.SocietyAssignments
                .Where(x => x.SocietyId == society.SocietyId &&
                            x.SessionId == sessionId &&
                            x.IsMentor == true)
                .Join(db.Teacher,
                      a => a.TeacherId,
                      t => t.userID,
                      (a, t) => new
                      {
                          TeacherId = t.userID,
                          TeacherName = t.name,
                          SocietyId = a.SocietyId,
                          SocietyName = society.SocietyName
                      })
                .ToList();

            return Ok(new
            {
                IsChairperson = true,
                SocietyId = society.SocietyId,
                SocietyName = society.SocietyName,
                Mentors = mentors
            });
        }


        [HttpGet]
        [Route("GetChairpersons/{sessionId}")]
        public IHttpActionResult GetChairpersons( int sessionId)
        {
            var data = db.SocietyAssignments
                .Where(a => a.SessionId == sessionId
                         && a.IsChairperson == true)
                .Join(db.Teacher,
                      a => a.TeacherId,
                      t => t.userID,
                      (a, t) => new
                      {
                          SocietyId = a.SocietyId,
                          SocietyName = a.Societies.SocietyName,
                          SessionId = a.SessionId,
                          TeacherId = a.TeacherId,
                          TeacherName = t.name
                      })
                .ToList(); // 👈 ONLY ONE CHAIRPERSON

            return Ok(data);
        }
    }
}
