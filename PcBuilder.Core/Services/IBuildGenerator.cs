using System.Collections.Generic;
using PcBuilder.Core.Models;

namespace PcBuilder.Core.Services
{
    public interface IBuildGenerator
    {
        List<BuildResult> Generate(decimal budget, WorkloadProfile profile);

    }
}
