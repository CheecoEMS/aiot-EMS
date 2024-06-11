using System.Data;
using System.Xml;

//IEC61970公用信息模型CIM 在电力系统网络交换模型中的应用

namespace EMS
{
    class SGCIM
    {
        /*
         （1）根据本文所提供的方法在关系数据库中创建变电站表

        CREATE TABLE[SUBSTATION](
        [ID][varchar](200)//变电站ID号 
        [NAME] [varchar] (200)//变电站名称 
        [ALIASNAME] [varchar]///变电站别名 
        [SUBSTATIONTYPE] [varchar] (200)//变电站类型,
        [MEMBEROF_SUBCONTROLAREA] [varchar] (200) //变电站所属的子控制区
        [DDATE] [datetime]//数据导入日期  
        )

        （2）读取CIM文件
            XmlDocument xmlDoc=new XmlDocument();
                string xmlfilename =”电网CIM模型.xml”;
            xmlDoc.Load(xmlfilename);
        （3）读取XML节点的数据

            XmlNode xn=xmlDoc.DocumentElement;
            XmlNodeList xnl = xn.ChildNodes;
                    string devicetype = "";
                    DataTable tblSUBSTATION;
            foreach(XmlNode xnf in xnl){
            XmlElement xe = (XmlElement)xnf;
                    devicetype=xe.LocalName.ToUpper();
            if(devicetype=="SUBSTATION")
            {
            procSubStation(xe, tblSUBSTATION);
            continue;
            }        
        } 
         */

        //解析变电站数据变写入数据库表
        static void procSubStation(XmlElement xe, DataTable tbl)
        {
            string id = ""; string name = ""; string descr = ""; string aliasName = ""; string psrtype = "";
            id = xe.GetAttribute("rdf:ID");
            XmlNodeList xnf1 = xe.ChildNodes;
            foreach (XmlNode xn2 in xnf1)
            {
                if (xn2.LocalName.ToLower() == "identifiedobject.name")
                    name = xn2.InnerText;
                if (xn2.LocalName.ToLower() == "identifiedobject.description") descr = xn2.InnerText;
                if (xn2.LocalName.ToLower() == "identifiedobject.aliasname") aliasName = xn2.InnerText;
                if (xn2.LocalName.ToLower() == "powersystemresource.psr-type")
                {
                    XmlElement xnn = (XmlElement)xn2;
                    psrtype = xnn.GetAttribute("rdf:resource");
                }
            }
            DataRow drCurrent = tbl.NewRow();
            drCurrent["id"] = id;
            drCurrent["name"] = name;
            drCurrent["aliasName"] = aliasName;
            drCurrent["psrTYPE"] = psrtype;
            drCurrent["ddate"] = System.DateTime.Now;
            tbl.Rows.Add(drCurrent);
        }


    }
}
