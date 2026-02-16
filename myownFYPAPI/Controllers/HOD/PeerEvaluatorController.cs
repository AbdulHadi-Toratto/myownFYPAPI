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
                    t.department
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
    }
}