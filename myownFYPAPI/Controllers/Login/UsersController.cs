using ExcelDataReader;
using myownFYPAPI.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Routing;
using System.Web.Http;
using static System.Net.WebRequestMethods;


namespace myownFYPAPI.Controllers.Login
{
    [RoutePrefix ("api/users")]
    public class UsersController : ApiController
    {
        fypapiv1Entities db = new fypapiv1Entities();

        [HttpPost]
        [Route("Login")]
        public HttpResponseMessage Login(String id ,String password)
        {
            var result = db.Users.FirstOrDefault(u => u.id  == id && u.password == password && u.isActive == 1);
            if(result == null)
            {
                return Request.CreateResponse(HttpStatusCode.NotFound, "No User Found");
            }
            else
            {
                return Request.CreateResponse(HttpStatusCode.OK, new { message = "Login Successful", role = result.role ,userId = result.id});
            }
        }
    }
}