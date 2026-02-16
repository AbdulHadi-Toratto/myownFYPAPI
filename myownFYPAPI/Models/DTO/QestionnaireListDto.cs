using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace myownFYPAPI.Models.DTO
{
    public class QestionnaireListDto
    {
        public int Id { get; set; }
        public string Type { get; set; }
        public string Flag { get; set; }
        public int QuestionCount { get; set; }
    }
}