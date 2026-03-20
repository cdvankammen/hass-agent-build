using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HASS.Agent.Enums;
using HASS.Agent.Models.Internal;
using Serilog;

namespace HASS.Agent.Extensions
{
    internal static class MediaRouteExtensions
    {
        internal static MediaRequest ParseApiMediaRequest(this NameValueCollection queryString)
        {
            if (queryString == null || queryString.Count == 0) return new MediaRequest();

            // Prefer the first recognized key — iterate all keys and pick the first one that maps to a MediaRequestType
            string key = null;
            foreach (var k in queryString.Keys.Cast<string>())
            {
                if (string.IsNullOrEmpty(k)) continue;
                var t = k.EnumMemberToEnum<MediaRequestType>();
                if (t != MediaRequestType.Unknown)
                {
                    key = k;
                    break;
                }
            }

            if (key == null) return new MediaRequest();

            // get the request type
            var requestType = key.EnumMemberToEnum<MediaRequestType>();

            // get its value
            var requestValue = queryString.Get(key) ?? string.Empty;

            // handle the type accordingly
            switch (requestType)
            {
                case MediaRequestType.Request:
                    var request = requestValue.EnumMemberToEnum<MediaPlayerRequest>();
                    return new MediaRequest().SetRequest(request);

                case MediaRequestType.Command:
                    var command = requestValue.EnumMemberToEnum<MediaPlayerCommand>();
                    return new MediaRequest().SetCommand(command);

                case MediaRequestType.PlayMedia:
                    return new MediaRequest().SetPlayMedia(requestValue);

                case MediaRequestType.Unknown:
                default:
                    Log.Warning("[LOCALAPI] Unable to parse request type: {type}", key);
                    return new MediaRequest();
            }
        }
    }
}
