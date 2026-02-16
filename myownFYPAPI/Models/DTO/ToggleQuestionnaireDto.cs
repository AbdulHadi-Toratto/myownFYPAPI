using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace myownFYPAPI.Models.DTO
{
    public class ToggleQuestionnaireDto
    {
        public int QuestionnaireId { get; set; }
        public bool TurnOn { get; set; }
    }
}