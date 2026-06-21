using System.Xml;
using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.Services;

/// <summary>Exports Gantt data to MS Project XML format.</summary>
public static class GanttXmlExporter
{
    private const string Ns = "http://schemas.microsoft.com/project";

    public static string Export(IEnumerable<TmWorkItem> tasks, IEnumerable<GanttDependency> dependencies)
    {
        var taskList = tasks.ToList();
        var depList  = dependencies.ToList();

        var settings = new XmlWriterSettings { Indent = true, Encoding = System.Text.Encoding.UTF8 };
        using var sw = new System.IO.StringWriter();
        using var xw = XmlWriter.Create(sw, settings);

        xw.WriteStartDocument();
        xw.WriteStartElement("Project", Ns);

        // Tasks
        xw.WriteStartElement("Tasks", Ns);
        for (var i = 0; i < taskList.Count; i++)
        {
            var task = taskList[i];
            xw.WriteStartElement("Task", Ns);
            xw.WriteElementString("UID",             Ns, (i + 1).ToString());
            xw.WriteElementString("ID",              Ns, (i + 1).ToString());
            xw.WriteElementString("Name",            Ns, task.Title);
            xw.WriteElementString("Start",           Ns, task.Start.ToString("yyyy-MM-ddTHH:mm:ss"));
            xw.WriteElementString("Finish",          Ns, task.End.ToString("yyyy-MM-ddTHH:mm:ss"));
            xw.WriteElementString("PercentComplete", Ns, task.PercentComplete.ToString());
            if (!string.IsNullOrEmpty(task.ParentId))
            {
                var parentIdx = taskList.FindIndex(t => t.Id == task.ParentId);
                if (parentIdx >= 0)
                    xw.WriteElementString("OutlineLevel", Ns, "1");
            }
            xw.WriteEndElement(); // Task
        }
        xw.WriteEndElement(); // Tasks

        // Dependencies as predecessors
        if (depList.Count > 0)
        {
            xw.WriteStartElement("Dependencies", Ns);
            foreach (var dep in depList)
            {
                var fromIdx = taskList.FindIndex(t => t.Id == dep.FromId) + 1;
                var toIdx   = taskList.FindIndex(t => t.Id == dep.ToId)   + 1;
                if (fromIdx <= 0 || toIdx <= 0) continue;

                xw.WriteStartElement("Dependency", Ns);
                xw.WriteElementString("PredecessorUID", Ns, fromIdx.ToString());
                xw.WriteElementString("SuccessorUID",   Ns, toIdx.ToString());
                xw.WriteElementString("Type",           Ns, dep.Type.ToString());
                xw.WriteElementString("Lag",            Ns, dep.LagDays.ToString());
                xw.WriteEndElement();
            }
            xw.WriteEndElement(); // Dependencies
        }

        xw.WriteEndElement(); // Project
        xw.WriteEndDocument();
        xw.Flush();
        return sw.ToString();
    }
}
