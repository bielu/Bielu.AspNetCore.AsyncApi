using LEGO.AsyncAPI.Models;

namespace Saunter.Options.Filters;

public interface IChannelFilter
{
    void Apply(AsyncApiChannel channel, ChannelFilterContext context);
}