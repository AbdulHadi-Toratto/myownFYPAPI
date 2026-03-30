using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using myownFYPAPI.Models;
using myownFYPAPI.Models.DTO;
using System.Transactions;

namespace myownFYPAPI.Controllers.HOD
{
    [RoutePrefix ("api/kpi")]
    public class KpiController : ApiController
    {
        fypapiv1Entities db = new fypapiv1Entities ();

        // 1. CREATE KPI WITH WEIGHTS
        [HttpPost]
        [Route("create-with-weight")]
        public IHttpActionResult CreateWithWeight(AddKPIDto dto)
        {
            if (dto == null || dto.SubKPIs == null || dto.SubKPIs.Count == 0)
                return BadRequest("Data incomplete.");

            decimal mainKpiTargetWeight = (decimal)dto.RequestedKPIWeight;
            decimal subKpiTotalInput = dto.SubKPIs.Sum(s => (decimal)s.Weight);

            if (mainKpiTargetWeight >= 100)
                return BadRequest("Main KPI weight must be less than 100.");

            try
            {
                using (var scope = new TransactionScope())
                {
                    // 1. Create Main KPI
                    KPI kpi = new KPI { name = dto.KPIName, KPI_Employeetype = dto.EmployeeTypeId };
                    db.KPI.Add(kpi);
                    db.SaveChanges(); // kpi.id generate ho gayi

                    decimal subFactor = subKpiTotalInput > 0 ? mainKpiTargetWeight / subKpiTotalInput : 0;

                    foreach (var subDto in dto.SubKPIs)
                    {
                        // 2. Create SubKPI
                        var subObj = new SubKPI { KPIID = kpi.id, name = subDto.Name };
                        db.SubKPI.Add(subObj);
                        db.SaveChanges(); // subObj.id generate ho gayi

                        // 3. IMPORTANT: Create Mapping in EmployeSessionKPI
                        // Ye wahi entry hai jo aapki SQL query mein miss thi
                        db.EmployeSessionKPI.Add(new EmployeSessionKPI
                        {
                            KPIID = kpi.id,
                            SubKPIID = subObj.id,
                            SessionID = dto.SessionId,
                            EmployeeTypeID = dto.EmployeeTypeId
                        });

                        // 4. Set Session Weight
                        decimal adjustedSubWeight = (decimal)subDto.Weight * subFactor;
                        db.SessionKPIWeight.Add(new SessionKPIWeight
                        {
                            SessionID = dto.SessionId,
                            KPIID = kpi.id,
                            SubKPIID = subObj.id,
                            Weight = (int)Math.Round(adjustedSubWeight, MidpointRounding.AwayFromZero)
                        });
                    }
                    db.SaveChanges();

                    // 5. Local Rounding Correction
                    var currentKpiWeights = db.SessionKPIWeight
                        .Where(w => w.SessionID == dto.SessionId && w.KPIID == kpi.id).ToList();

                    int currentKpiSum = currentKpiWeights.Sum(w => w.Weight ?? 0);

                    if (currentKpiSum != (int)mainKpiTargetWeight && currentKpiWeights.Any())
                    {
                        currentKpiWeights.First().Weight += ((int)mainKpiTargetWeight - currentKpiSum);
                        db.SaveChanges();
                    }

                    // 6. Global Adjustment (Baaki KPIs ke weights adjust karna)
                    AdjustGlobalWeights(dto.SessionId, dto.EmployeeTypeId, kpi.id, mainKpiTargetWeight);

                    scope.Complete();
                    return Ok(new { Message = "KPI Saved, Mapping Created, and weights adjusted.", Status = "Success" });
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception("Error: " + ex.Message));
            }
        }

        // 2. ADD SUB-KPI TO EXISTING KPI (Dynamic Adjustment)
        [HttpPost]
        [Route("add-subkpi-dynamic")]
        public IHttpActionResult AddSubKpiDynamic(DynamicSubKpiDto dto)
        {
            try
            {
                using (var scope = new TransactionScope())
                {
                    var existingWeights = db.SessionKPIWeight.Where(w => w.SessionID == dto.SessionId && w.KPIID == dto.KpiId).ToList();
                    if (!existingWeights.Any()) return BadRequest("KPI not found.");

                    // Scaling logic
                    int mainKpiTotal = existingWeights.Sum(x => x.Weight ?? 0);
                    decimal factor = (decimal)(mainKpiTotal - dto.NewWeight) / mainKpiTotal;

                    foreach (var w in existingWeights)
                    {
                        w.Weight = (int)Math.Round((w.Weight ?? 0) * factor, MidpointRounding.AwayFromZero);
                    }

                    // 1. Create SubKPI
                    SubKPI newSub = new SubKPI { KPIID = dto.KpiId, name = dto.Name };
                    db.SubKPI.Add(newSub);
                    db.SaveChanges();

                    // 2. Create Mapping Entry - IMPORTANT
                    var kpiRecord = db.KPI.Find(dto.KpiId);
                    db.EmployeSessionKPI.Add(new EmployeSessionKPI
                    {
                        KPIID = dto.KpiId,
                        SubKPIID = newSub.id,
                        SessionID = dto.SessionId,
                        EmployeeTypeID = kpiRecord?.KPI_Employeetype
                    });

                    // 3. Add Weight
                    db.SessionKPIWeight.Add(new SessionKPIWeight
                    {
                        SessionID = dto.SessionId,
                        KPIID = dto.KpiId,
                        SubKPIID = newSub.id,
                        Weight = dto.NewWeight
                    });

                    db.SaveChanges();
                    scope.Complete();
                    return Ok("New Sub-KPI added with mapping.");
                }
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }

        // 3. DELETE SUB-KPI (With Auto-Adjustment)
        [HttpDelete]
        [Route("delete-subkpi/{sid}/{subid}")]
        public IHttpActionResult DeleteSubKpi(int sid, int subid)
        {
            try
            {
                using (var scope = new TransactionScope())
                {
                    var weightRec = db.SessionKPIWeight.FirstOrDefault(w => w.SubKPIID == subid && w.SessionID == sid);
                    if (weightRec == null) return NotFound();

                    int kpiId = (int)weightRec.KPIID;
                    int kpiTotalWeight = db.SessionKPIWeight.Where(w => w.KPIID == kpiId && w.SessionID == sid).Sum(x => x.Weight ?? 0);

                    // 1. Remove from Mapping Table - IMPORTANT
                    var mapRec = db.EmployeSessionKPI.FirstOrDefault(m => m.SubKPIID == subid && m.SessionID == sid);
                    if (mapRec != null) db.EmployeSessionKPI.Remove(mapRec);

                    // 2. Remove Weight and SubKPI definition
                    db.SessionKPIWeight.Remove(weightRec);
                    var subDef = db.SubKPI.Find(subid);
                    if (subDef != null) db.SubKPI.Remove(subDef);

                    db.SaveChanges();

                    // 3. Re-adjust remaining SubKPI weights within the same KPI
                    var remaining = db.SessionKPIWeight.Where(w => w.KPIID == kpiId && w.SessionID == sid).ToList();
                    if (remaining.Any())
                    {
                        decimal currentSum = remaining.Sum(x => (decimal)(x.Weight ?? 0));
                        if (currentSum > 0)
                        {
                            decimal factor = (decimal)kpiTotalWeight / currentSum;
                            foreach (var o in remaining)
                            {
                                o.Weight = (int)Math.Round((o.Weight ?? 0) * factor, MidpointRounding.AwayFromZero);
                            }
                            db.SaveChanges();
                            // Final sum check
                            int finalSum = remaining.Sum(x => x.Weight ?? 0);
                            if (finalSum != kpiTotalWeight) { remaining.First().Weight += (kpiTotalWeight - finalSum); db.SaveChanges(); }
                        }
                    }
                    scope.Complete();
                    return Ok("Sub-KPI deleted and weights re-distributed.");
                }
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }

        // 4. DELETE MAIN KPI (Safe & Global 100%)
        [HttpDelete]
        [Route("delete-main-kpi/{sid:int}/{kpiid:int}")] // Added explicit type constraints
        public IHttpActionResult DeleteMainKpi(int sid, int kpiid)
        {
            try
            {
                using (var scope = new TransactionScope())
                {
                    var kpi = db.KPI.Find(kpiid);
                    if (kpi == null) return Content(HttpStatusCode.NotFound, "KPI not found in database.");

                    int empTypeId = kpi.KPI_Employeetype ?? 0;

                    // 1. Remove from Mapping Table
                    var mappings = db.EmployeSessionKPI.Where(m => m.KPIID == kpiid && m.SessionID == sid).ToList();
                    foreach (var m in mappings) db.EmployeSessionKPI.Remove(m);

                    // 2. Remove Weights
                    var weights = db.SessionKPIWeight.Where(w => w.KPIID == kpiid && w.SessionID == sid).ToList();
                    foreach (var w in weights) db.SessionKPIWeight.Remove(w);

                    // 3. Remove SubKPIs (only those belonging to this KPI)
                    var subs = db.SubKPI.Where(s => s.KPIID == kpiid).ToList();
                    foreach (var s in subs) db.SubKPI.Remove(s);

                    db.KPI.Remove(kpi);
                    db.SaveChanges();

                    // 4. Global Weight Adjustment
                    var bakiWeights = db.SessionKPIWeight.Where(w => w.SessionID == sid &&
                                      db.KPI.Any(k => k.id == w.KPIID && k.KPI_Employeetype == empTypeId)).ToList();

                    if (bakiWeights.Any())
                    {
                        decimal currentTotal = bakiWeights.Sum(x => (decimal)(x.Weight ?? 0));
                        if (currentTotal > 0)
                        {
                            decimal factor = 100m / currentTotal;
                            foreach (var bw in bakiWeights)
                            {
                                bw.Weight = (int)Math.Round((bw.Weight ?? 0) * factor, MidpointRounding.AwayFromZero);
                            }
                            db.SaveChanges();

                            int finalSum = bakiWeights.Sum(x => x.Weight ?? 0);
                            if (finalSum != 100)
                            {
                                bakiWeights.First().Weight += (100 - finalSum);
                                db.SaveChanges();
                            }
                        }
                    }

                    scope.Complete();
                    return Ok(new { Message = "Deleted Successfully" });
                }
            }
            catch (Exception ex)
            {
                // This will help you see the EXACT C# error in your React Native console
                return BadRequest(ex.InnerException?.Message ?? ex.Message);
            }
        }
        // 5. EDIT MAIN KPI NAME
        [HttpPut]
        [Route("edit-kpi-name/{id}")]
        public IHttpActionResult EditKpiName(int id, [FromBody] string newName)
        {
            if (string.IsNullOrEmpty(newName)) return BadRequest("Name required.");
            var kpi = db.KPI.Find(id);
            if (kpi == null) return NotFound();
            kpi.name = newName;
            db.SaveChanges();
            return Ok("KPI updated.");
        }

        // 6. EDIT SUB-KPI NAME
        [HttpPut]
        [Route("edit-subkpi-name/{id}")]
        public IHttpActionResult EditSubKpiName(int id, [FromBody] string newName)
        {
            if (string.IsNullOrEmpty(newName)) return BadRequest("Name required.");
            var sub = db.SubKPI.Find(id);
            if (sub == null) return NotFound();
            sub.name = newName;
            db.SaveChanges();
            return Ok("Sub-KPI updated.");
        }

        // 7. HELPER: Global Adjustment
        private void AdjustGlobalWeights(int sessionId, int empTypeId, int currentKpiId, decimal newKpiWeight)
        {
            var existingWeights = db.SessionKPIWeight
                .Where(w => w.SessionID == sessionId && w.KPIID != currentKpiId &&
                            db.KPI.Any(k => k.id == w.KPIID && k.KPI_Employeetype == empTypeId)).ToList();

            if (existingWeights.Any())
            {
                decimal currentOldTotal = existingWeights.Sum(w => (decimal)(w.Weight ?? 0));
                decimal targetForOld = 100m - newKpiWeight;

                if (currentOldTotal > 0)
                {
                    decimal globalFactor = targetForOld / currentOldTotal;
                    foreach (var w in existingWeights)
                    {
                        w.Weight = (int)Math.Round((w.Weight ?? 0) * globalFactor, MidpointRounding.AwayFromZero);
                    }
                    db.SaveChanges();
                }

                var all = db.SessionKPIWeight.Where(w => w.SessionID == sessionId &&
                            db.KPI.Any(k => k.id == w.KPIID && k.KPI_Employeetype == empTypeId)).ToList();
                int totalSum = all.Sum(x => x.Weight ?? 0);
                if (totalSum != 100)
                {
                    existingWeights.First().Weight += (100 - totalSum);
                    db.SaveChanges();
                }
            }
        }

        // 8. GET METHODS
        [HttpGet]
        [Route("view-weights/{sid}/{eid}")]
        public IHttpActionResult GetWeights(int sid, int eid)
        {
            var res = db.KPI.Where(k => k.KPI_Employeetype == eid).ToList()
                .Select(k => new
                {
                    kpiId = k.id,
                    kpiName = k.name,
                    totalKpiWeight = db.SessionKPIWeight.Where(w => w.SessionID == sid && w.KPIID == k.id).Sum(w => (int?)w.Weight) ?? 0,
                    subKpis = (from w in db.SessionKPIWeight
                               join s in db.SubKPI on w.SubKPIID equals s.id
                               where w.SessionID == sid && w.KPIID == k.id
                               select new { subKpiId = s.id, subKpiName = s.name, weight = w.Weight }).ToList()
                }).Where(x => x.totalKpiWeight > 0).ToList();
            return Ok(res);
        }

        [HttpGet][Route("sessions")] public IHttpActionResult GetSessions() => Ok(db.Session.Select(s => new { s.id, s.name }).ToList());
        [HttpGet][Route("emptypes")] public IHttpActionResult GetEmpTypes() => Ok(db.EmployeeType.Select(e => new { e.id, e.type }).ToList());

    }
}
