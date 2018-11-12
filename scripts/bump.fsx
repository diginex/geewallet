#!/usr/bin/env fsharpi

open System
open System.IO
#load "Infra.fs"
open FSX.Infrastructure

let rootDir = DirectoryInfo(Path.Combine(__SOURCE_DIRECTORY__, ".."))
let fullVersion = Misc.GetCurrentVersion(rootDir)
let androidVersion = fullVersion.MinorRevision

let newVersion = int androidVersion + 1
let newFullVersion = Version(sprintf "%s.%s.%s.%s"
                                 (fullVersion.Major.ToString())
                                 (fullVersion.Minor.ToString())
                                 (fullVersion.MajorRevision.ToString())
                                 (newVersion.ToString()))

let replaceScript = Path.Combine(__SOURCE_DIRECTORY__, "replace.fsx")
Process.Execute (sprintf "%s %s %s"
                         replaceScript
                         (fullVersion.ToString())
                         (newFullVersion.ToString()),
                 false, true)
Process.Execute (sprintf "%s \\\"%s\\\" \\\"%s\\\""
                         replaceScript
                         (androidVersion.ToString())
                         (newVersion.ToString()),
                 false, true)

Process.Execute (sprintf "git add src/GWallet.Backend.Tests/*.json",
                 false, true)
Process.Execute (sprintf "git add src/GWallet.Backend/Properties/CommonAssemblyInfo.fs",
                 false, true)
Process.Execute (sprintf "git add src/GWallet.Frontend.XF.Android/Properties/AndroidManifest.xml",
                 false, true)
Process.Execute (sprintf "git commit -m \"Bump version: %s -> %s\"" (fullVersion.ToString()) (newFullVersion.ToString()),
                 false, true)
Process.Execute (sprintf "git tag %s" (newFullVersion.ToString()),
                 false, true)
Console.WriteLine sprintf "Version bumped. Remember to push via `git push <remote> <branch> && git push <remote> %s`"
                          (newFullVersion.ToString())
