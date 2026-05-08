using myownFYPAPI.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Routing;
using System.Web.Http;
using System.Data.Entity;
using myownFYPAPI.Models.DTO;
namespace myownFYPAPI.Controllers.Director
{
    [RoutePrefix("api/Questionnaire")]

    public class QuestionnaireController : ApiController
    {
        fypapiv1Entities db = new fypapiv1Entities();

        [Route("Create")]
        public IHttpActionResult CreateQuestionnaire(QuestionCreateDto model)
        {
            if (model == null || model.Questions == null || model.Questions.Count == 0)
            {
                return BadRequest("Invalid data");
            }

            // 1️⃣ Create Questionnaire
            var questionnaire = new Questionare
            {
                type = model.EvaluationType,
                flag = "0" // DEFAULT — DO NOT CHANGE
            };

            db.Questionare.Add(questionnaire);
            db.SaveChanges(); // 🔥 ID generated here

            // 2️⃣ Insert Questions
            foreach (var qObj in model.Questions) // model.Questions ab object list hai
            {
                if (!string.IsNullOrWhiteSpace(qObj.QuestionText))
                {
                    db.Questions.Add(new Questions
                    {
                        QuestionareID = questionnaire.id,
                        QuestionText = qObj.QuestionText,
                        isCritical = qObj.IsCritical // <--- Ye naya column add karein
                    });
                }
            }

            db.SaveChanges();

            return Ok(new
            {
                message = "Questionnaire saved successfully",
                QuestionnaireId = questionnaire.id
            });
        }

        [HttpGet]
        [Route("GetAll")]
        public IHttpActionResult GetAll()
        {
            var data = db.Questionare
                .Select(q => new QestionnaireListDto
                {
                    Id = q.id,
                    Type = q.type,
                    Flag = q.flag,
                    QuestionCount = q.Questions.Count()
                })
                .ToList();

            return Ok(data);
        }

        [HttpPost]
        [Route("Toggle")]
        public IHttpActionResult ToggleQuestionnaire(ToggleQuestionnaireDto model)
        {
            var questionnaire = db.Questionare.Find(model.QuestionnaireId);

            if (questionnaire == null)
                return NotFound();

            if (model.TurnOn)
            {
                // ❌ Check if same type is already ON
                bool alreadyActive = db.Questionare.Any(q =>
                    q.type == questionnaire.type &&
                    q.flag == "1" &&
                    q.id != questionnaire.id
                );

                if (alreadyActive)
                {
                    return BadRequest("Another evaluation of this type is already active.");
                }

                questionnaire.flag = "1";
            }
            else
            {
                questionnaire.flag = "0";
            }

            db.SaveChanges();

            return Ok(new { message = "Status updated successfully" });
        }

        [HttpPost]
        [Route("SaveAllChanges")]
        public IHttpActionResult SaveAllChanges(SaveQuestionnaireChangesDto model)
        {
            if (model == null)
                return BadRequest("Invalid data");
            try
            {


                // 1️⃣ DELETE REMOVED QUESTIONS
                if (model.DeletedIds != null && model.DeletedIds.Count > 0)
                {
                    var deleteQuestions = db.Questions
                        .Where(q => model.DeletedIds.Contains(q.QuestionID))
                        .ToList();

                    foreach (var q in deleteQuestions)
                    {
                        db.Questions.Remove(q);
                    }

                    //db.Questions.RemoveRange(deleteQuestions);
                }

                // 2️⃣ ADD & UPDATE QUESTIONS
                if (model.Questions != null)
                {
                    foreach (var q in model.Questions)
                    {
                        if (q.Id == 0) // NEW
                        {
                            if (!string.IsNullOrWhiteSpace(q.QuestionText))
                            {
                                db.Questions.Add(new Questions
                                {
                                    QuestionareID = model.QuestionnaireId,
                                    QuestionText = q.QuestionText,
                                    isCritical = q.IsCritical // <--- Yahan add karein
                                });
                            }
                        }
                        else // UPDATE
                        {
                            var existing = db.Questions.Find(q.Id);
                            if (existing != null)
                            {
                                existing.QuestionText = q.QuestionText;
                                existing.isCritical = q.IsCritical; // <--- Status update karein
                            }
                        }
                    }
                }

                db.SaveChanges();
                return Ok(new { message = "Changes saved successfully" });

            }
            catch (Exception ex)
            {
                return BadRequest(ex.InnerException?.Message ?? ex.Message);
            }
        }



        [HttpGet]
        [Route("GetById/{id}")]
        public IHttpActionResult GetById(int id)
        {
            var questionnaire = db.Questionare
                .Where(q => q.id == id)
                .Select(q => new
                {
                    id = q.id,
                    title = q.type, // You don't have a separate title field, so using 'type' here
                    evaluationType = q.type,
                    questions = q.Questions.Select(qq => new
                    {
                        id = qq.QuestionID,
                        questionText = qq.QuestionText,
                          isCritical = qq.isCritical
                    })
                })
                .FirstOrDefault();

            if (questionnaire == null)
                return NotFound();

            return Ok(questionnaire);
        }

        [HttpDelete]
        [Route("Delete/{id}")]
        public IHttpActionResult DeleteQuestionnaire(int id)
        {
            try
            {
                var questionnaire = db.Questionare.Include(q => q.Questions).FirstOrDefault(q => q.id == id);

                if (questionnaire == null)
                    return NotFound();

                // Pehle iske saare sawal delete karein
                if (questionnaire.Questions != null && questionnaire.Questions.Any())
                {
                    db.Questions.RemoveRange(questionnaire.Questions);
                }

                // Phir main header delete karein
                db.Questionare.Remove(questionnaire);
                db.SaveChanges();

                return Ok(new { message = "Questionnaire and all its questions deleted successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest("Cannot delete: " + (ex.InnerException?.Message ?? ex.Message));
            }
        }

        [HttpDelete]
        [Route("DeleteQuestion/{id}")]
        public IHttpActionResult DeleteIndividualQuestion(int id)
        {
            try
            {
                var question = db.Questions.Find(id);
                if (question == null)
                    return NotFound();

                db.Questions.Remove(question);
                db.SaveChanges();

                return Ok(new { message = "Question removed successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest("Error deleting question: " + ex.Message);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                db.Dispose();

            base.Dispose(disposing);
        }
    }


}
