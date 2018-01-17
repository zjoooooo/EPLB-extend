using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EPLB_extend
{
    public partial class Form1 : Form
    {

        public Dictionary<string, string> carparklist = new Dictionary<string, string>();
        public Dictionary<string, string> batchlist = new Dictionary<string, string>();
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            init();

            Thread thr = new Thread(() => ReadEPLB());
            thr.Start();
        }
        
        private void init()
        {
            batchlist.Clear();
            carparklist.Clear();
            string constr = "Data Source=172.16.1.89;uid=secure;pwd=weishenme;database=carpark";
            string CommandText = @"select name,ip,batch from Whole where batch in('B26','B28','B30')";
           //string CommandText = @"select name,ip from Whole where name='GMLM' ";
            DataSet ds = null;
            try
            {
                ds = SqlHelper.ExecuteDataset(constr, CommandType.Text, CommandText);
                foreach (DataRow ls in ds.Tables[0].Rows)
                {
                    carparklist.Add(ls[0].ToString(), ls[1].ToString());
                    batchlist.Add(ls[0].ToString(), ls[2].ToString());
                }

            }
            catch (SqlException e)
            {
                LogClass.WriteLog("Fail To Get Car Park List : " + e.ToString());
            }
            finally
            {
                try
                {
                    if (ds != null)
                        ds.Dispose();
                }
                catch (SqlException e)
                {
                    LogClass.WriteLog("Fail To Close Car Park List DataSet : " + e.ToString());
                }
            }
        }

        private void ReadEPLB()
        {
            string cmd = @"SELECT * FROM [dbo].[season_mst] where holder_type=13 and s_status=2 ORDER BY date_to;
                           SELECT * from station_setup where station_type in (1,2);";
            foreach (KeyValuePair<string,string> cp in carparklist)
            {          
                //LogClass.WriteLog($"======={cp.Key}========");
                string constr = "Data Source=" + cp.Value + ";uid=sa;pwd=yzhh2007;database=" + cp.Key;
                DataSet ds = null;
                try
                {
                    ds = SqlHelper.ExecuteDataset(constr,CommandType.Text,cmd);
                }catch(SqlException sqle)
                {
                    LogClass.WriteLog($"Reading EPLB season error {sqle.ToString()}");
                    continue;
                }

                if (ds == null)
                {
                    continue;
                }


                foreach(DataRow dr in ds.Tables[0].Rows)
                {
                    string IU = dr["season_no"].ToString();
                    string Date_From = dr["date_from"].ToString();
                    string Date_To = dr["date_to"].ToString();
                    string holder_name = dr["holder_name"].ToString();
                    string vehicle_no = dr["vehicle_no"].ToString();
                    DateTime dt_to = Convert.ToDateTime(Date_To);
                    DateTime dt = Convert.ToDateTime("2017-12-31 00:00");
                    if (dt == dt_to)
                    {
                        LogClass.WriteLog($"CP={cp.Key},IU={IU},Expired Date={Date_To} need to extend 2 more years.");
                        //Update expired date 2 more years.
                        DateTime NewExpiredDate = dt_to.AddYears(2);
                        //Generate update sql cmd
                        string updateParameter = "Update season_mst SET date_to=@NewExpiredDate,s_status=1,";

                        for (int i = 1; i <= ds.Tables[1].Rows.Count; i++)
                        {
                            if (i == ds.Tables[1].Rows.Count)
                            {
                                updateParameter += $"s{i}_fetched=0 ";
                            }
                            else
                            {
                                updateParameter += $"s{i}_fetched=0,";
                            }
                        }
                        updateParameter += "WHERE season_no=@season_no";
                        LogClass.WriteLog($"Final paramterSql={updateParameter}");

                        SqlParameter[] para = new SqlParameter[]
                        {
                            new SqlParameter("@NewExpiredDate",NewExpiredDate.ToString("yyyy-MM-dd HH:mm:ss")),
                            new SqlParameter("@season_no",IU)

                        };

                        //Update 2 cmd at PMS db. 1st updateParameter  2nd updateTasklist
                        try
                        {
                            SqlHelper.ExecuteNonQuery(constr, CommandType.Text, updateParameter,para);
                            LogClass.WriteLog($"Update updateParameter ok for {IU}");
                        }
                        catch (SqlException e)
                        {
                            LogClass.WriteLog($"Fail To update updateParameter! {e.ToString()}");
                            continue;
                        }
                    }
                    else
                    {
                        LogClass.WriteLog($"CP={cp.Key},IU={IU},Vehicle={vehicle_no},holder_name={holder_name},Date_from={Date_From.Substring(0,18)},Date_To={Date_To.Substring(0, 18)}");
                    }
                }


            }



        }

    }
}
