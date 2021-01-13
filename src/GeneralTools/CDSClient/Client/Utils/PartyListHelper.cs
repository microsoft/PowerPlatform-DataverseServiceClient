using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerPlatform.Cds.Client.Utils
{
	internal static class PartyListHelper
	{
		/// <summary>
		/// Participation types for activity parties.
		/// </summary>
		internal static class ParticipationType
		{
			public const int Sender = 1;
			public const int Recipient = 2;
			public const int CCRecipient = 3;
			public const int BccRecipient = 4;
			public const int RequiredAttendee = 5;
			public const int OptionalAttendee = 6;
			public const int Organizer = 7;
			public const int Regarding = 8;
			public const int Owner = 9;
			public const int Resource = 10;
			public const int Customer = 11;
			public const int Partner = 12;
		}

		/// <summary>
		/// This dictionary holds the mapping for the attribute names and their PartiticipationType values.
		/// This is currently used to determine the attribute related to a given activity party row
		/// for enforcing FLS.
		/// We will need to update this mapping when an attribute is made as a partylist attribute
		/// </summary>
		internal static Dictionary<string, int> PartyListMap = new Dictionary<string, int>
		{
			{ "from", ParticipationType.Sender },
			{ "to", ParticipationType.Recipient },
			{ "cc", ParticipationType.CCRecipient },
			{ "bcc", ParticipationType.BccRecipient },
			{ "requiredattendees", ParticipationType.RequiredAttendee },
			{ "optionalattendees", ParticipationType.OptionalAttendee },
			{ "organizer", ParticipationType.Organizer },
			{ "regardingobjectid", ParticipationType.Regarding },
			{ "owner", ParticipationType.Owner },
			{ "resources", ParticipationType.Resource },
			{ "customer", ParticipationType.Customer },
			{ "customers", ParticipationType.Customer },
			{ "partner", ParticipationType.Partner },
			{ "partners", ParticipationType.Partner }
		};


		/// <summary>
		/// Map attributeKeyName to Participation Type
		/// </summary>
		/// <param name="partyKeyName"></param>
		/// <returns></returns>
		internal static int GetParticipationtypeMasks(string partyKeyName)
		{
			int mask;
			if (PartyListMap.TryGetValue(partyKeyName, out mask))
				return mask;
			else
				return -1;
		}
	}
}
