using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.SqlServer.Management.SqlParser.SqlCodeDom;

namespace SqlDataCompare.Core
{
    public static class SqlStatementExtensions
    {
        public static bool TryParseSelectColumns(this SqlStatement statement, out string[] columns)
        {
            var colList = new List<string>();
            columns = colList.ToArray();

            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(statement.Xml);

            //Narrow to only the Select. There otherwise could be a CTE or something else containing SqlSelectClause
            try
            {
                xmlDoc.LoadXml(xmlDoc.GetElementsByTagName("SqlSelectSpecification")[0].OuterXml);
            }
            catch
            {
                return false; //No select
            }

            var select = xmlDoc.GetElementsByTagName("SqlSelectClause");
            if (select.Count == 0)
            {
                return false;
            }
            xmlDoc.LoadXml(select[0].OuterXml.ToString());

            foreach (XmlNode node in select[0].ChildNodes)
            {
                if (node.Name == "SqlSelectScalarExpression")
                {
                    string alias = node.Attributes["Alias"]?.Value;
                    if (alias != null)
                    {
                        colList.Add(alias);
                        continue;
                    }
                    else
                    {
                        var col = node.FirstChild.NextSibling.Attributes["ColumnOrPropertyName"]?.Value;

                        if (col != null)
                        {
                            colList.Add(col);
                            continue;
                        }

                        col = node.FirstChild.NextSibling.Attributes["ColumnName"]?.Value;

                        if (col != null)
                        {
                            colList.Add(col);
                            continue;
                        }

                        colList.Add(null);
                        continue;
                    }
                }
                else if (node.Name == "SqlSelectStarExpression")
                {
                    colList.Add("*");
                    continue;
                }
            }

            columns = colList.ToArray();
            return true;
        }

        public static bool TryParseIndexForInto(this SqlStatement statement, out int index)
        {
            var colList = new List<string>();
            index = 0;

            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(statement.Xml);

            //Narrow to only the Select. There otherwise could be a CTE or something else containing SqlSelectClause
            try
            {
                xmlDoc.LoadXml(xmlDoc.GetElementsByTagName("SqlSelectSpecification")[0].OuterXml);
            }
            catch
            {
                return false; //No select
            }

            var select = xmlDoc.GetElementsByTagName("SqlSelectClause");
            if (select.Count == 0)
            {
                return false;
            }
            xmlDoc.LoadXml(select[0].OuterXml.ToString());

            foreach (XmlNode node in select[0].ChildNodes)
            {
                if (node.Name == "SqlSelectScalarExpression")
                {
                    string alias = node.Attributes["Alias"]?.Value;
                    if (alias != null)
                    {
                        colList.Add(alias);
                        continue;
                    }
                    else
                    {
                        var col = node.FirstChild.NextSibling.Attributes["ColumnOrPropertyName"]?.Value;

                        if (col != null)
                        {
                            colList.Add(col);
                            continue;
                        }

                        col = node.FirstChild.NextSibling.Attributes["ColumnName"]?.Value;

                        if (col != null)
                        {
                            colList.Add(col);
                            continue;
                        }

                        colList.Add(null);
                        continue;
                    }
                }
                else if (node.Name == "SqlSelectStarExpression")
                {
                    colList.Add("*");
                    continue;
                }
            }

            return true;
        }
    }
}
