using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace myownFYPAPI.Models.DTO
{
    public class PerformanceDto
    {
        public int OverallPercentage { get; set; }   
        public int ObtainedPoints { get; set; }     
        public int TotalPoints { get; set; }        

        public List<KpiDto> Kpis { get; set; }
    }

    public class KpiDto
    {
        public string Name { get; set; }             // e.g. Academics
        public int Score { get; set; }               // e.g. 70
        public int Total { get; set; }               // e.g. 100

        public List<SubKpiDto> SubKpis { get; set; }
    }

    public class SubKpiDto
    {
        public string Name { get; set; }             // e.g. Student Evaluation
        public int Score { get; set; }               // e.g. 24
        public int Total { get; set; }               // e.g. 30
    }
}