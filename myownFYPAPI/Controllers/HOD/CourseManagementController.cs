using myownFYPAPI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Transactions;
using System.Web.Http;
using static myownFYPAPI.Models.DTO.CourseManagementDto;

namespace myownFYPAPI.Controllers.HOD
{
    [RoutePrefix("api/CourseManagement")]
    public class CourseManagementController : ApiController
    {

        fypapiv1Entities db = new fypapiv1Entities();

        [HttpGet]
        [Route("EnrollmentCourses/{sessionId}")]
        public IHttpActionResult GetEnrollmentCourses(int sessionId)
        {
            var data = (from e in db.Enrollment
                        join t in db.Teacher
                            on e.teacherID equals t.userID
                        join c in db.Course
                            on e.courseCode equals c.code
                        where e.sessionID == sessionId
                        select new
                        {
                            id = e.id,
                            teacher = t.name,
                            teacherID = t.userID,
                            course = c.title,
                            code = e.courseCode
                        }).ToList();

            return Ok(data);
        }







        // 2. POST: Evaluate Submission (Handles Paper and Folder)
        [HttpPost]
        [Route("SaveEvaluation")]
        public IHttpActionResult SaveEvaluation(EvaluationRequestDTO dto)
        {
            if (dto == null || dto.Evaluations == null || !dto.Evaluations.Any())
                return BadRequest("Invalid Data");

            try
            {
                using (var scope = new TransactionScope())
                {
                    int total = dto.Evaluations.Count;

                    //  FIX: Har course ke marks jodo
                    int totalPaperEarned = 0;
                    int totalFolderEarned = 0;

                    foreach (var eval in dto.Evaluations)
                    {
                        // On-time = 5, Late = 2
                        totalPaperEarned += eval.PaperStatus.ToLower().Contains("on-time") ? 5 : 2;
                        totalFolderEarned += eval.FolderStatus.ToLower().Contains("on-time") ? 5 : 2;
                    }

                    // FIX: Sum ki bajaye average lo
                    // Example: 3 courses sab on-time  → (5+5+5)/3 = 5 
                    // Example: 3 courses sab late      → (2+2+2)/3 = 2 
                    // Example: 2 courses 1 each        → (5+2)/2   = 4 
                    int paperScore = (int)Math.Round((double)totalPaperEarned / total);
                    int folderScore = (int)Math.Round((double)totalFolderEarned / total);

                    // FIX: Contains ki bajaye direct ID comparison — EF error nahi dega
                    // Paper ke purane scores delete karo
                    var paperMapping = db.EmployeSessionKPI.FirstOrDefault(m =>
                        m.SubKPI.name.Contains("Paper") && m.SessionID == dto.SessionID);

                    if (paperMapping != null)
                    {
                        int paperMappingId = paperMapping.id;
                        var oldPaper = db.KPIScore
                            .Where(s => s.empID == dto.TeacherID && s.empKPIID == paperMappingId)
                            .ToList();
                        db.KPIScore.RemoveRange(oldPaper);
                    }

                    // Folder ke purane scores delete karo
                    var folderMapping = db.EmployeSessionKPI.FirstOrDefault(m =>
                        m.SubKPI.name.Contains("Folder") && m.SessionID == dto.SessionID);

                    if (folderMapping != null)
                    {
                        int folderMappingId = folderMapping.id;
                        var oldFolder = db.KPIScore
                            .Where(s => s.empID == dto.TeacherID && s.empKPIID == folderMappingId)
                            .ToList();
                        db.KPIScore.RemoveRange(oldFolder);
                    }

                    // Pehle delete save karo
                    db.SaveChanges();

                    //  Naye averaged scores save karo (ek record per SubKPI)
                    UpsertScore(dto.TeacherID, dto.SessionID, "Paper Submission", paperScore, dto.HODID);
                    UpsertScore(dto.TeacherID, dto.SessionID, "Folder Submission", folderScore, dto.HODID);

                    db.SaveChanges();
                    scope.Complete();

                    return Ok(new
                    {
                        message = "Evaluation saved successfully!",
                        paperScore,
                        folderScore,
                        totalCourses = total
                    });
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception("Backend Error: " + ex.Message));
            }
        }

        // FIX: CourseCode parameter hata diya — ek hi record per teacher per SubKPI
        private void UpsertScore(string tid, int sid, string subKpiName, int scoreValue, string hodId)
        {
            var mapping = db.EmployeSessionKPI.FirstOrDefault(m =>
                          m.SubKPI.name.Contains(subKpiName) && m.SessionID == sid);

            if (mapping == null) return;

            var existingScore = db.KPIScore.FirstOrDefault(s =>
                                s.empKPIID == mapping.id && s.empID == tid);

            if (existingScore != null)
            {
                existingScore.score = scoreValue;
                existingScore.evaluatorID = hodId;
            }
            else
            {
                db.KPIScore.Add(new KPIScore
                {
                    empKPIID = mapping.id,
                    empID = tid,
                    score = scoreValue,
                    evaluatorID = hodId
                });
            }
        }

        // 4. GET: Teacher Performance/Remarks for Teacher Login
        [HttpGet]
        [Route("my-Courseperformance/{tid}/{sid}")]
        public IHttpActionResult GetTeacherRemarks(string tid, int sid)
        {
            try
            {
                var performance = (from s in db.KPIScore
                                   join m in db.EmployeSessionKPI on s.empKPIID equals m.id
                                   join sub in db.SubKPI on m.SubKPIID equals sub.id
                                   where s.empID == tid && m.SessionID == sid
                                   select new
                                   {
                                       Activity = sub.name,
                                       ObtainedScore = s.score,
                                       Status = s.score == 5 ? "On Time" : "Late",
                                       Remarks = s.score == 5
                                           ? "Excellent! Submitted on time."
                                           : "Delayed submission recorded."
                                   }).ToList();

                if (!performance.Any())
                    return NotFound();

                return Ok(performance);
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception("Fetch performance error: " + ex.Message));
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
