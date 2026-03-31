using myownFYPAPI.Models;
using myownFYPAPI.Models.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Routing;

namespace myownFYPAPI.Controllers.Teacher
{
    [RoutePrefix("api/teacher/performance")]
    public class OwnPerformanceController : ApiController
    {

        int employeeTypeId;

        [Route("SeeOwnPerformance")]
        public IHttpActionResult GetTeacherPerformance(string userId, int sessionId)
        {
            fypapiv1Entities db = new fypapiv1Entities();
            var response = new PerformanceDto();
            var kpiList = new List<KpiDto>();
            
            // 🔹 STEP 1: Get Employee Type ID from userId
            var role = db.Teacher
                .Where(u => u.userID == userId)
                .Select(u => u.department)
                .FirstOrDefault();

            if (role == "CS")
            {
                employeeTypeId = 1;
            }else if (role == "Non CS")
            {
                employeeTypeId = 2;
            }

            var kpiIds = db.EmployeSessionKPI
            .Where(e => e.SessionID == sessionId && e.EmployeeTypeID == employeeTypeId)
            .Select(e => e.KPIID)
            .Distinct()
            .ToList();


            // 🔹 STEP 2: Get KPIs for this employee type
            var kpis = db.KPI
                    .Where(k => kpiIds.Contains(k.id))
                    .ToList();

            int overallScore = 0;
            int overallWeight = 0;

            foreach (var kpi in kpis)
            {
                var subKpiIds = db.EmployeSessionKPI
                .Where(e => e.KPIID == kpi.id &&
                e.SessionID == sessionId &&
                e.EmployeeTypeID == employeeTypeId)
                .Select(e => e.SubKPIID)
                .ToList();

                var subKpis = db.SubKPI
                 .Where(s => subKpiIds.Contains(s.id))
                 .ToList();

                int kpiScore = 0;
                int kpiTotal = 0;

                var subKpiDtos = new List<SubKpiDto>();

                foreach (var sub in subKpis)
                {
                    double avg = 0;

                    // 🔹 STUDENT
                    if (sub.name == "Student Evaluation")
                    {
                        var scores = db.StudentEvaluation
                            .Where(se => se.SessionID == sessionId &&
                                db.Enrollment.Any(e =>
                                    e.id == se.enrollmentID &&
                                    e.teacherID == userId))
                            .Select(se => (int?)se.score)
                            .ToList();

                        avg = scores.Count > 0 ? scores.Average() ?? 0 : 0;
                    }

                    // 🔹 PEER
                    else if (sub.name == "Peer Evaluation")
                    {
                        var scores = db.PeerEvaluation
                            .Where(pe => pe.evaluateeID == userId)
                            .Select(pe => (int?)pe.score)
                            .ToList();

                        avg = scores.Count > 0 ? scores.Average() ?? 0 : 0;
                    }

                    // 🔹 OTHER
                    else
                    {
                        var scores = db.KPIScore
                            .Where(s => s.empID == userId && s.empKPIID == sub.id)
                            .Select(s => (int?)s.score)
                            .ToList();

                        avg = scores.Count > 0 ? scores.Average() ?? 0 : 0;
                    }

                    // 🔹 Sub KPI weight
                   int  weight = db.SessionKPIWeight
                        .Where(w => w.SubKPIID == sub.id && w.SessionID == sessionId)
                        .Select(w => w.Weight)
                        .FirstOrDefault() ?? 0;

                    // 🔹 Convert to marks (max = 4)
                    int finalScore = (int)Math.Round((avg / 4.0) * weight);

                    subKpiDtos.Add(new SubKpiDto
                    {
                        Name = sub.name,
                        Score = finalScore,
                        Total = weight
                    });

                    kpiScore += finalScore;
                    kpiTotal += weight;
                }

                // 🔹 Main KPI weight (80%, 20%)
                int kpiWeight = db.SessionKPIWeight
                    .Where(w => w.KPIID == kpi.id && w.SessionID == sessionId && w.SubKPIID == null)
                    .Select(w => w.Weight)
                    .FirstOrDefault() ?? 0;

                double kpiPercentage = kpiTotal > 0 ? (double)kpiScore / kpiTotal : 0;
                int weightedKpiScore = (int)Math.Round(kpiPercentage * kpiWeight);

                overallScore += kpiScore;
                overallWeight += kpiTotal;

                kpiList.Add(new KpiDto
                {
                    Name = kpi.name,
                    Score = kpiScore,
                    Total = kpiTotal,
                    SubKpis = subKpiDtos
                });
            }

            response.Kpis = kpiList;
            response.OverallPercentage = overallWeight > 0
                ? (int)Math.Round((double)overallScore * 100 / overallWeight)
                : 0;

            response.ObtainedPoints = overallScore;
            response.TotalPoints = overallWeight;

            return Ok(response);
        }

    }
}
