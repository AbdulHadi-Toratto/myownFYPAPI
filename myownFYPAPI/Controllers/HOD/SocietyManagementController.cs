using myownFYPAPI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using static System.Net.WebRequestMethods;

namespace myownFYPAPI.Controllers.HOD
{
    [RoutePrefix("api/SocietyManagement")]
    public class SocietyManagementController : ApiController
    {
        fypapiv1Entities db = new fypapiv1Entities();

        [HttpPost]
        [Route("AddSociety")]
        public IHttpActionResult AddSociety([FromBody] myownFYPAPI.Models.DTO.SocietyDTO model)
        {
            if (model == null)
                return BadRequest("Invalid data");

            var society = new myownFYPAPI.Models.Societies
            {
                SocietyName = model.SocietyName,
                Description = model.Description
            };

            db.Societies.Add(society);
            db.SaveChanges();

            // OPTIONAL: if you want session mapping, add here

            return Ok(new { message = "Society added successfully" });
        }
        // =========================
        // 2. GET ALL SOCIETIES
        // =========================
        [HttpGet]
        [Route("GetAll")]
        public IHttpActionResult GetAll()
        {
            var data = db.Societies
                .Select(s => new
                {
                    s.SocietyId,
                    s.SocietyName,
                    s.Description,

                    // Chair count
                    ChairCount = db.SocietyAssignments
                        .Count(a => a.SocietyId == s.SocietyId && a.IsChairperson == true),

                    // Mentor count
                    MentorCount = db.SocietyAssignments
                        .Count(a => a.SocietyId == s.SocietyId && a.IsMentor == true),

                    // Chairperson names
                    Chairpersons = (from a in db.SocietyAssignments
                                    join t in db.Teacher on a.TeacherId equals t.userID
                                    where a.SocietyId == s.SocietyId
                                       && a.IsChairperson == true
                                    select t.name).ToList(),

                    // Mentor names (optional but useful)
                    Mentors = (from a in db.SocietyAssignments
                               join t in db.Teacher on a.TeacherId equals t.userID
                               where a.SocietyId == s.SocietyId
                                  && a.IsMentor == true
                               select t.name).ToList()
                })
                .ToList();

            return Ok(data);
        }

        [HttpGet]
        [Route("GetBySession/{sessionId}")]
        public IHttpActionResult GetBySession(int sessionId)
        {
            var data = (from s in db.Societies
                        join a in db.SocietyAssignments
                        on s.SocietyId equals a.SocietyId
                        where a.SessionId == sessionId
                        select new
                        {
                            s.SocietyId,
                            s.SocietyName,
                            s.Description
                        }).Distinct().ToList();

            return Ok(data);
        }

        [HttpGet]
        [Route("GetTeachers")]
        public IHttpActionResult GetTeachers()
        {
            var data = db.Teacher
                .Select(t => new
                {
                    t.userID,
                    t.name,
                    //t.designation
                }).ToList();

            return Ok(data);
        }

        [HttpPost]
        [Route("AssignTeacher")]
        public IHttpActionResult AssignTeacher([FromBody] myownFYPAPI.Models.DTO.SocietyAssignment model)
        {
            if (model == null)
                return BadRequest("Invalid data");

            // =========================================
            // STEP 1: REMOVE ALL OLD CHAIRPERSONS
            // (IMPORTANT: fixes duplicate problem permanently)
            // =========================================
            var oldChairs = db.SocietyAssignments
                .Where(x =>
                    x.SocietyId == model.SocietyId &&
                    x.SessionId == model.SessionId &&
                    x.IsChairperson == true)
                .ToList();

            if (oldChairs.Any())
            {
                db.SocietyAssignments.RemoveRange(oldChairs);
            }

            // =========================================
            // STEP 2: ADD NEW CHAIRPERSON
            // =========================================
            var newChair = new SocietyAssignments
            {
                TeacherId = model.TeacherId,
                SocietyId = model.SocietyId,
                SessionId = model.SessionId,
                IsChairperson = true,
                IsMentor = false
            };

            db.SocietyAssignments.Add(newChair);

            db.SaveChanges();

            return Ok(new { message = "Chairperson updated successfully" });
        }

        [HttpGet]
        [Route("GetChairpersons/{societyId}/{sessionId}")]
        public IHttpActionResult GetChairpersons(int societyId, int sessionId)
        {
            var data = db.SocietyAssignments
                .Where(a => a.SocietyId == societyId
                         && a.SessionId == sessionId
                         && a.IsChairperson == true)
                .Join(db.Teacher,
                      a => a.TeacherId,
                      t => t.userID,
                      (a, t) => new
                      {
                          SocietyId = a.SocietyId,
                          SessionId = a.SessionId,
                          TeacherId = a.TeacherId,
                          TeacherName = t.name
                      })
                .FirstOrDefault(); // 👈 ONLY ONE CHAIRPERSON

            return Ok(data);
        }


        [HttpPost]
        [Route("AssignMentorsBulk")]
        public IHttpActionResult AssignMentorsBulk([FromBody] List<myownFYPAPI.Models.DTO.SocietyAssignment> models)
        {
            if (models == null || !models.Any())
                return BadRequest("Invalid data");

            foreach (var model in models)
            {
                var exists = db.SocietyAssignments.FirstOrDefault(x =>
                    x.SocietyId == model.SocietyId &&
                    x.SessionId == model.SessionId &&
                    x.TeacherId == model.TeacherId &&
                    x.IsMentor == true);

                if (exists == null)
                {
                    db.SocietyAssignments.Add(new SocietyAssignments
                    {
                        TeacherId = model.TeacherId,
                        SocietyId = model.SocietyId,
                        SessionId = model.SessionId,
                        IsChairperson = false,
                        IsMentor = true
                    });
                }
            }

            db.SaveChanges();

            return Ok(new { message = "Mentors assigned successfully" });
        }

        //[HttpGet]
        //[Route("GetMentors/{societyId}")]
        //public IHttpActionResult GetMentors(int societyId)
        //{
        //    var data = db.SocietyAssignments
        //        .Where(x => x.SocietyId == societyId && x.IsMentor == true)
        //        .ToList();

        //    return Ok(data);
        //}

        [HttpGet]
        [Route("GetMentors/{societyId}")]
        public IHttpActionResult GetMentors(int societyId)
        {
            var data = db.SocietyAssignments
                .Where(x => x.SocietyId == societyId && x.IsMentor == true)
                .Select(x => new
                {
                    TeacherId = x.TeacherId,
                    SocietyId = x.SocietyId,
                    SessionId = x.SessionId
                })
                .ToList();

            return Ok(data);
        }

    }
}
