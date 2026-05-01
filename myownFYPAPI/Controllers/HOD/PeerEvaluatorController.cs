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


namespace myownFYPAPI.Controllers.HOD
{
    [RoutePrefix("api/PeerEvaluator")]
    public class PeerEvaluatorController : ApiController
    {
        fypapiv1Entities db = new fypapiv1Entities();

        [HttpGet]
        [Route("GetTeachers")]
        public IHttpActionResult GetTeachers()
        {
            var teachers = db.Teacher
                .Select(t => new
                {
                    t.userID,
                    t.name,
                    t.department,
                    t.isPermanentEvaluator
                })
                .ToList();

            return Ok(teachers);
        }


        [HttpPost]
        [Route("Add")]
        public IHttpActionResult AddPeerEvaluators(AddPeerEvaluatorDTO model)
        {
            if (model == null || model.TeacherIds == null || model.TeacherIds.Count == 0)
                return BadRequest("Invalid data");

            int sessionId = model.SessionId;

            foreach (var teacherId in model.TeacherIds)
            {
                string teacherIdStr = teacherId.ToString();

                bool alreadyExists = db.PeerEvaluator.Any(pe =>
                    pe.teacherID == teacherIdStr &&
                    pe.sessionID == sessionId
                );

                if (!alreadyExists)
                {
                    db.PeerEvaluator.Add(new PeerEvaluator
                    {
                        teacherID = teacherIdStr,
                        sessionID = sessionId
                    });
                }
            }

            db.SaveChanges();
            return Ok("Peer evaluators added successfully");
        }

        [HttpGet]
        [Route("BySession/{sessionId}")]
        public IHttpActionResult GetPeerEvaluatorsBySession(int sessionId)
        {
            var evaluators = (from pe in db.PeerEvaluator
                              join t in db.Teacher on pe.teacherID equals t.userID
                              where pe.sessionID == sessionId
                              select new
                              {
                                  userID = t.userID,
                                  name = t.name,
                                  department = t.department
                              }).ToList();

            return Ok(evaluators);
        }

        [HttpPost]
        [Route("TogglePermanent")]
        public IHttpActionResult TogglePermanentStatus(TogglePermanentDto model)
        {
            if (model == null || string.IsNullOrEmpty(model.UserID))
                return BadRequest("Invalid user data.");

            try
            {
                var teacher = db.Teacher.FirstOrDefault(t => t.userID == model.UserID);
                if (teacher == null) return NotFound();

                // Status update karein: 1 for Permanent, 0 for Normal
                teacher.isPermanentEvaluator = model.IsPermanent ? 1 : 0;

                // Agar kisi ko Permanent banaya hai, toh usey PeerEvaluator table (manual list) se hatayein
                // Kyunke wo ab globally available hoga
                if (model.IsPermanent)
                {
                    var manualAssignments = db.PeerEvaluator.Where(pe => pe.teacherID == model.UserID).ToList();
                    if (manualAssignments.Any())
                    {
                        db.PeerEvaluator.RemoveRange(manualAssignments);
                    }
                }

                db.SaveChanges();

                string status = model.IsPermanent ? "Permanent" : "Normal";
                return Ok(new { message = $"Teacher marked as {status} evaluator successfully." });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }


        [HttpPost]
        [Route("SetBulkPermanent")]
        public IHttpActionResult SetBulkPermanent(BulkPermanentDto model)
        {
            if (model == null || model.UserIDs == null || !model.UserIDs.Any())
                return BadRequest("No User IDs provided.");

            try
            {
                var teachers = db.Teacher.Where(t => model.UserIDs.Contains(t.userID)).ToList();

                foreach (var t in teachers)
                {
                    t.isPermanentEvaluator = 1;
                }

                db.SaveChanges();

                return Ok(new { message = "Selected teachers are now Permanent Evaluators." });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }
    }
}