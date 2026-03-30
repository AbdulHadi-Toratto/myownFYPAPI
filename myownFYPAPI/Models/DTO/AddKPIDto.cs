using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace myownFYPAPI.Models.DTO
{
    public class AddKPIDto
    {
        public int SessionId { get; set; }
        public string KPIName { get; set; }
        public int EmployeeTypeId { get; set; }
        public int RequestedKPIWeight { get; set; }

        public List<SubKPIDto> SubKPIs { get; set; }
    }

    public class SubKPIDto
    {
        public String Name { get; set; }
        public int Weight { get; set; }
    }

    public class DynamicSubKpiDto
    {
        public int SessionId { get; set; }
        public int KpiId { get; set; }
        public string Name { get; set; }
        public int NewWeight { get; set; }
    }
}