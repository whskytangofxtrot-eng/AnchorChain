using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace AnchorChain.Preloader
{
    // Applied after either loader path has supplied its AnchorChain assembly.
    // Keeps successful loading and dependency ordering intact, but makes the
    // existing "Aborting chainload" failure path actually leave LoadPlugins.
    public static class AnchorChainStartupGuard
    {
        private const string FailureMessage = "Aborting chainload; unable to load any more plugins:";
        public static Action StartupStopped;

        public static void Apply(Type loaderType)
        {
            var method = loaderType.GetMethod("LoadPlugins", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (method == null || method.ReturnType != typeof(void) || method.GetParameters().Length != 0)
                throw new InvalidOperationException("Unsupported AnchorChain startup method; the retry guard could not be installed.");
            new Harmony("io.github.whskytangofxtrot.anchorchain.startup_guard").Patch(method,
                transpiler: new HarmonyMethod(typeof(AnchorChainStartupGuard), nameof(StopNoProgressRetry)));
        }

        public static void ReportStopped() { if (StartupStopped != null) StartupStopped(); }

        public static IEnumerable<CodeInstruction> StopNoProgressRetry(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.Select(i => new CodeInstruction(i)).ToList();
            var markers = Enumerable.Range(0, code.Count)
                .Where(index => code[index].opcode == OpCodes.Ldstr &&
                    code[index].operand is string && ((string)code[index].operand).StartsWith(FailureMessage, StringComparison.Ordinal))
                .ToArray();
            if (markers.Length != 1) throw new InvalidOperationException("AnchorChain retry guard: expected exactly one no-progress error path.");
            int marker = markers[0], log = -1;
            for (int i = marker + 1; i < Math.Min(code.Count, marker + 16); i++)
            {
                var called = code[i].operand as MethodInfo;
                if ((code[i].opcode == OpCodes.Call || code[i].opcode == OpCodes.Callvirt) && called != null &&
                    called.DeclaringType.FullName == "BepInEx.Logging.ManualLogSource" && called.Name == "LogError" &&
                    called.ReturnType == typeof(void)) { log = i; break; }
            }
            if (log < 0) throw new InvalidOperationException("AnchorChain retry guard: the error logger was not found.");
            int branch = log + 1;
            while (branch < code.Count && code[branch].opcode == OpCodes.Nop) branch++;
            if (branch >= code.Count || (code[branch].opcode != OpCodes.Br && code[branch].opcode != OpCodes.Br_S) ||
                !(code[branch].operand is Label))
                throw new InvalidOperationException("AnchorChain retry guard: the retry branch changed.");
            var target = (Label)code[branch].operand;
            int destination = code.FindIndex(i => i.labels.Contains(target));
            if (destination < 0 || destination >= marker)
                throw new InvalidOperationException("AnchorChain retry guard: expected a backward retry branch.");
            int depth = 0;
            for (int i = 0; i <= branch; i++) foreach (var block in code[i].blocks)
            {
                if (block.blockType == ExceptionBlockType.BeginExceptionBlock) depth++;
                else if (block.blockType == ExceptionBlockType.EndExceptionBlock) depth--;
            }
            if (depth != 0) throw new InvalidOperationException("AnchorChain retry guard: cannot return from a protected block.");

            // Preserve the branch's labels, and retain the original error and
            // exception logging. Never fall through to the success message.
            code[branch].opcode = OpCodes.Call;
            code[branch].operand = typeof(AnchorChainStartupGuard).GetMethod(nameof(ReportStopped));
            code.Insert(branch + 1, new CodeInstruction(OpCodes.Ret));
            return code;
        }
    }
}
