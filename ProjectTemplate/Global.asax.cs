using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Web.Routing;

namespace ProjectTemplate
{
    public class WebApiApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            GlobalConfiguration.Configure(WebApiConfig.Register);
        }
    }

    //dashboard linking with databaseionPercent": 92 }


[WebMethod]
public string GetDashboardStats()
{
    MySqlConnection conn = new MySqlConnection(getConString());
    conn.Open();

    string query = @"
        SELECT 
            (SELECT COUNT(*) FROM response) AS totalResponses,
            (SELECT COUNT(DISTINCT employee_id) FROM response) AS uniqueResponders
    ";

    MySqlCommand cmd = new MySqlCommand(query, conn);
    MySqlDataReader reader = cmd.ExecuteReader();

    var result = new {
        completionPercent = 0
    };

    if (reader.Read())
    {
        int total = Convert.ToInt32(reader["totalResponses"]);
        int users = Convert.ToInt32(reader["uniqueResponders"]);

        if (users > 0)
        {
            result = new {
                completionPercent = (total / (double)users) * 100
            };
        }
    }

    conn.Close();

    return new JavaScriptSerializer().Serialize(result);

}
}
