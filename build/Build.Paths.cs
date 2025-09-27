using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nuke.Common.IO;

partial class Build
{
    private static AbsolutePath SourceDirectory => RootDirectory / "src";

    private static AbsolutePath ArtifactsDirectory => RootDirectory / "Artifacts";
}

