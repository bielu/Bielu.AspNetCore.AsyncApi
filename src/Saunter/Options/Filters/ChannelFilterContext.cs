using System.Reflection;
using Bielu.AspNetCore.AsyncApi.Attributes.Attributes;

namespace Saunter.Options.Filters;

public class ChannelFilterContext
{
    public ChannelFilterContext(MemberInfo member, ChannelAttribute channel)
    {
        Member = member;
        Channel = channel;
    }

    public MemberInfo Member { get; }

    public ChannelAttribute Channel { get; }
}
