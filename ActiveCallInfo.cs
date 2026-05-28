using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GrabacionesAgente
{
	public class ActiveCallInfo
	{
		public string CallId { get; set; }

		public string AgentId { get; set; }

		public string AgentEmailId { get; set; }

		public string AgentName { get; set; }

		public string Ani { get; set; }

		public string Dnis { get; set; }

		public string QueueId { get; set; }

		public string QueueName { get; set; }

		public string TeamName { get; set; }

		public DateTime StartTime { get; set; }

		public DateTime? EndTime { get; set; }

		public string RawJson { get; set; }
	}
}
