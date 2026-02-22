using System;
using System.Web.Services;
using System.Web.Script.Services;
using MySql.Data.MySqlClient;

namespace ProjectTemplate
{
    [WebService(Namespace = "http://tempuri.org/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]
    [ScriptService]
    public class ProjectServices : WebService
    {
        // DB creds
        private string dbID = "cis440Spring2026team8";
        private string dbPass = "cis440Spring2026team8";
        private string dbName = "cis440Spring2026team8";

        private string getConString()
        {
            return "SERVER=107.180.1.16; PORT=3306; DATABASE=" + dbName + "; UID=" + dbID + "; PASSWORD=" + dbPass;
        }

        // IMPORTANT: use PROPERTIES (not fields) so ASP.NET JSON serializes cleanly
        public class LoginResult
        {
            public bool success { get; set; }
            public string message { get; set; }
        }

        [WebMethod(EnableSession = true)]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string TestConnection()
        {
            return "Success!";
        }

        [WebMethod(EnableSession = true)]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public LoginResult EmployeeLogin(string email, string password)
        {
            LoginResult result = new LoginResult();

            try
            {
                string sql = @"
                    SELECT employee_ID
                    FROM employee
                    WHERE email = @email
                      AND password_hash = SHA2(@password, 256)
                    LIMIT 1;
                ";

                using (MySqlConnection con = new MySqlConnection(getConString()))
                using (MySqlCommand cmd = new MySqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@email", email);
                    cmd.Parameters.AddWithValue("@password", password);

                    con.Open();
                    object employeeIdObj = cmd.ExecuteScalar();

                    if (employeeIdObj == null)
                    {
                        result.success = false;
                        result.message = "Incorrect email or password. Try again.";
                        return result;
                    }

                    // Session for auth
                    Session["loggedIn"] = true;
                    Session["employeeId"] = Convert.ToInt32(employeeIdObj);

                    // Anonymous ID (use this later for feedback)
                    if (Session["anonId"] == null)
                        Session["anonId"] = Guid.NewGuid().ToString();

                    result.success = true;
                    result.message = "Login successful.";
                    return result;
                }
            }
            catch (Exception ex)
            {
                result.success = false;
                result.message = "Server error: " + ex.Message;
                return result;
            }
        }
    }
}