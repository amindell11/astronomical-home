import * as fs from "node:fs";
import * as os from "node:os";
import * as path from "node:path";
import { StringEnum } from "@mariozechner/pi-ai";
import type { ExtensionAPI } from "@mariozechner/pi-coding-agent";
import { Type } from "@sinclair/typebox";

type ScopeType = "feature" | "module" | "workspace";
type Strategy = "auto" | "full" | "failed" | "smoke" | "targeted";
type Platform = "Both" | "EditMode" | "PlayMode";

interface ScopeSelection {
  testFilter?: string;
  testCategory?: string;
  assemblyNames?: string;
}

interface ScopeConfig {
  smoke?: ScopeSelection;
  features?: Record<string, ScopeSelection>;
  modules?: Record<string, ScopeSelection>;
}

function resolvePath(cwd: string, p: string): string {
  return path.isAbsolute(p) ? p : path.join(cwd, p);
}

function readJsonSafe<T>(filePath: string): T | null {
  try {
    if (!fs.existsSync(filePath)) return null;
    return JSON.parse(fs.readFileSync(filePath, "utf8")) as T;
  } catch {
    return null;
  }
}

function flattenFailures(summary: any, limit = 50): Array<Record<string, string>> {
  const out: Array<Record<string, string>> = [];
  const runs = Array.isArray(summary?.runs) ? summary.runs : [];
  for (const run of runs) {
    const platform = String(run?.platform ?? "unknown");
    const failures = Array.isArray(run?.failures) ? run.failures : [];
    for (const f of failures) {
      out.push({
        platform,
        name: String(f?.name ?? ""),
        fullName: String(f?.fullName ?? ""),
        message: String(f?.message ?? ""),
        topStack: String(f?.topStack ?? ""),
      });
      if (out.length >= limit) return out;
    }
  }
  return out;
}

function summarizeFailuresForText(failures: Array<Record<string, string>>, limit = 10): string {
  if (failures.length === 0) return "No failing tests.";
  const lines = failures.slice(0, limit).map((f) => {
    const stack = f.topStack ? ` @ ${f.topStack}` : "";
    return `- ${f.platform}: ${f.fullName || f.name}\n  ${f.message}${stack}`;
  });
  if (failures.length > limit) lines.push(`- ... and ${failures.length - limit} more`);
  return lines.join("\n");
}

function getShellAndArgs(scriptPath: string): { shell: string; prefix: string[] } {
  if (process.platform === "win32") {
    return {
      shell: "powershell",
      prefix: ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", scriptPath],
    };
  }
  return {
    shell: "pwsh",
    prefix: ["-NoProfile", "-File", scriptPath],
  };
}

function resolveSelection(
  config: ScopeConfig | null,
  scopeType: ScopeType,
  scopeName: string | undefined,
  strategy: Strategy,
): ScopeSelection {
  if (strategy === "smoke" && config?.smoke) return { ...config.smoke };

  if (scopeType === "feature" && scopeName && config?.features?.[scopeName]) {
    return { ...config.features[scopeName] };
  }

  if (scopeType === "module" && scopeName && config?.modules?.[scopeName]) {
    return { ...config.modules[scopeName] };
  }

  return {};
}

function writeTempPrompt(content: string): { dir: string; filePath: string } {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), "pi-bugsplat-"));
  const filePath = path.join(dir, "system.md");
  fs.writeFileSync(filePath, content, "utf8");
  return { dir, filePath };
}

async function runBugsplatSubagent(
  pi: ExtensionAPI,
  cwd: string,
  summaryPath: string,
  summary: any,
  failures: Array<Record<string, string>>,
): Promise<string> {
  const topFailures = failures.slice(0, 8);
  const systemPrompt = [
    "You are Bugsplat, a Unity test failure root-cause analyst.",
    "Goal: identify likely root causes quickly with minimal verbosity.",
    "Use repository evidence only.",
    "Output format:",
    "1) likely_causes: bullet list",
    "2) confidence: low/medium/high per cause",
    "3) files_to_inspect: file paths",
    "4) first_fix_candidate: one actionable next step",
  ].join("\n");

  const prompt = [
    `Repository cwd: ${cwd}`,
    `Unity summary path: ${summaryPath}`,
    `Totals: ${JSON.stringify(summary?.totals ?? {})}`,
    "Top failures:",
    JSON.stringify(topFailures, null, 2),
    "Analyze likely cause and likely files.",
  ].join("\n\n");

  const tmp = writeTempPrompt(systemPrompt);
  try {
    const result = await pi.exec(
      "pi",
      [
        "-p",
        "--no-session",
        "--tools",
        "read,grep,find,ls,bash",
        "--append-system-prompt",
        tmp.filePath,
        prompt,
      ],
      { timeout: 240000 },
    );

    const text = `${result.stdout || ""}${result.stderr ? `\n${result.stderr}` : ""}`.trim();
    if (!text) return "Bugsplat returned no output.";
    return text.length > 5000 ? `${text.slice(0, 5000)}\n...[truncated]` : text;
  } finally {
    try {
      fs.unlinkSync(tmp.filePath);
    } catch {}
    try {
      fs.rmdirSync(tmp.dir);
    } catch {}
  }
}

const RunParams = Type.Object({
  scopeType: Type.Optional(StringEnum(["feature", "module", "workspace"] as const, { default: "workspace" })),
  scopeName: Type.Optional(Type.String({ description: "Feature/module name for scope maps" })),
  strategy: Type.Optional(StringEnum(["auto", "full", "failed", "smoke", "targeted"] as const, { default: "auto" })),
  platform: Type.Optional(StringEnum(["Both", "EditMode", "PlayMode"] as const, { default: "Both" })),
  dispatchBugsplat: Type.Optional(Type.Boolean({ default: false })),
  projectPath: Type.Optional(Type.String({ description: "Unity project path" })),
  outDir: Type.Optional(Type.String({ description: "Output folder for XML/log/summary" })),
  unityPath: Type.Optional(Type.String({ description: "Unity executable path override" })),
  testFilter: Type.Optional(Type.String()),
  testCategory: Type.Optional(Type.String()),
  assemblyNames: Type.Optional(Type.String()),
  includeStackTrace: Type.Optional(Type.Boolean({ default: false })),
  maxFailures: Type.Optional(Type.Number({ default: 25 })),
  timeoutSec: Type.Optional(Type.Number({ default: 1800 })),
});

const BugsplatParams = Type.Object({
  summaryPath: Type.Optional(Type.String({ description: "Path to a unity summary JSON file" })),
  maxFailures: Type.Optional(Type.Number({ default: 8 })),
});

export default function unityTestLoopExtension(pi: ExtensionAPI): void {
  pi.registerTool({
    name: "unity_test_run",
    label: "Unity Test Run",
    description:
      "Run Unity EditMode/PlayMode tests for feature/module/workspace scopes, return compact JSON-oriented failure summaries, and optionally dispatch a bugsplat sub-agent.",
    parameters: RunParams,
    async execute(_id, params, _signal, onUpdate, ctx) {
      const cwd = ctx.cwd;
      const projectPath = resolvePath(cwd, params.projectPath || "src/Asteroids3D");
      const outDir = resolvePath(cwd, params.outDir || "results/unity-tests-agent");
      const scopeType: ScopeType = (params.scopeType || "workspace") as ScopeType;
      const strategy: Strategy = (params.strategy || "auto") as Strategy;
      const platform: Platform = (params.platform || "Both") as Platform;

      const scriptPath = resolvePath(cwd, "scripts/unity_test_agent.ps1");
      if (!fs.existsSync(scriptPath)) {
        return {
          content: [{ type: "text", text: `Missing test runner script: ${scriptPath}` }],
          isError: true,
        };
      }

      const scopeConfigPath = resolvePath(cwd, "scripts/unity_test_scopes.json");
      const scopeConfig = readJsonSafe<ScopeConfig>(scopeConfigPath);
      const mapped = resolveSelection(scopeConfig, scopeType, params.scopeName, strategy);

      let testFilter = params.testFilter || mapped.testFilter || "";
      let testCategory = params.testCategory || mapped.testCategory || "";
      let assemblyNames = params.assemblyNames || mapped.assemblyNames || "";
      const rerunFrom = resolvePath(outDir, "latest-summary.json");

      if (strategy === "full") {
        testFilter = "";
        testCategory = "";
        assemblyNames = "";
      }

      const shell = getShellAndArgs(scriptPath);
      const timeoutSec = Math.max(60, Math.floor(params.timeoutSec || 1800));
      const args = [...shell.prefix, "-ProjectPath", projectPath, "-OutDir", outDir, "-Mode", platform, "-UnityTimeoutSec", String(timeoutSec)];

      args.push("-ScopeType", scopeType.charAt(0).toUpperCase() + scopeType.slice(1));
      if (params.scopeName) args.push("-ScopeName", params.scopeName);
      if (params.unityPath) args.push("-UnityPath", resolvePath(cwd, params.unityPath));
      if (testFilter) args.push("-TestFilter", testFilter);
      if (testCategory) args.push("-TestCategory", testCategory);
      if (assemblyNames) args.push("-AssemblyNames", assemblyNames);
      if (params.maxFailures) args.push("-MaxFailures", String(Math.max(1, Math.floor(params.maxFailures))));
      if (params.includeStackTrace) args.push("-IncludeStackTrace");

      if ((strategy === "failed" || (strategy === "auto" && fs.existsSync(rerunFrom))) && fs.existsSync(rerunFrom)) {
        args.push("-RerunFailedFrom", rerunFrom);
      }

      onUpdate?.({ content: [{ type: "text", text: `Running Unity ${platform} tests (${scopeType}${params.scopeName ? `:${params.scopeName}` : ""}, timeout=${timeoutSec}s)...` }] });

      const startedAt = Date.now();
      let execResult: { stdout?: string; stderr?: string };
      try {
        execResult = await pi.exec(shell.shell, args, { timeout: (timeoutSec + 120) * 1000 });
      } catch (error) {
        const message = error instanceof Error ? error.message : String(error);
        return {
          content: [{ type: "text", text: `Unity runner execution failed or timed out after ~${timeoutSec}s. ${message}` }],
          isError: true,
          details: { timeoutSec, command: shell.shell, args },
        };
      }
      const combinedOut = `${execResult.stdout || ""}\n${execResult.stderr || ""}`;
      const match = combinedOut.match(/UNITY_TEST_SUMMARY_JSON=(.+)/);
      const summaryPath = match?.[1]?.trim() || resolvePath(outDir, "latest-summary.json");

      const summaryExists = fs.existsSync(summaryPath);
      const summaryMtime = summaryExists ? fs.statSync(summaryPath).mtimeMs : 0;
      const isFreshSummary = summaryExists && summaryMtime >= startedAt - 1000;

      if (!isFreshSummary) {
        return {
          content: [{ type: "text", text: `Unity runner did not produce a fresh summary file for this run: ${summaryPath}` }],
          isError: true,
          details: {
            summaryPath,
            summaryExists,
            summaryMtime,
            startedAt,
            shellOutput: combinedOut.slice(-4000),
          },
        };
      }

      const summary = readJsonSafe<any>(summaryPath);
      if (!summary) {
        return {
          content: [{ type: "text", text: `Unity runner finished but summary JSON was not found/readable: ${summaryPath}` }],
          isError: true,
          details: { shellOutput: combinedOut.slice(-4000), summaryPath },
        };
      }

      const failures = flattenFailures(summary, 80);
      const totals = summary.totals || {};
      const thin = {
        mode: summary.mode,
        status: summary.status,
        scopeType,
        scopeName: params.scopeName || "",
        strategy,
        platform,
        totals,
        failures,
        artifacts: {
          summaryPath,
          latestSummaryPath: resolvePath(outDir, "latest-summary.json"),
          outDir,
        },
      };

      let bugsplat: string | undefined;
      if (params.dispatchBugsplat && failures.length > 0) {
        onUpdate?.({ content: [{ type: "text", text: "Dispatching bugsplat sub-agent..." }] });
        try {
          bugsplat = await runBugsplatSubagent(pi, cwd, summaryPath, summary, failures);
        } catch (error) {
          bugsplat = `Bugsplat failed: ${error instanceof Error ? error.message : String(error)}`;
        }
      }

      const headline = `Unity tests ${summary.status}. total=${totals.total ?? 0} passed=${totals.passed ?? 0} failed=${totals.failed ?? 0}`;
      const failText = summarizeFailuresForText(failures);
      const infraRun = (Array.isArray(summary.runs) ? summary.runs : []).find((r: any) => r?.status === "infra_error");
      const infraNote = summary.status === "infra_error"
        ? `\n\nInfrastructure error. ${infraRun?.note ? `Note: ${infraRun.note}. ` : ""}${infraRun?.logTail ? "Check run logTail/details for root cause." : ""}`
        : "";
      const ask = summary.status === "infra_error"
        ? infraNote
        : failures.length > 0
          ? "\n\nWhich failures should I attempt to fix first? I can then rerun only that feature/module until it passes."
          : "\n\nAll selected tests passed.";

      return {
        content: [{ type: "text", text: `${headline}\n\n${failText}${ask}` }],
        details: {
          ...thin,
          bugsplat,
          shellTail: combinedOut.slice(-4000),
        },
        isError: summary.status === "infra_error",
      };
    },
  });

  pi.registerTool({
    name: "unity_test_bugsplat",
    label: "Unity Test Bugsplat",
    description: "Analyze failing Unity test summary with a dedicated sub-agent to identify likely root causes.",
    parameters: BugsplatParams,
    async execute(_id, params, _signal, _onUpdate, ctx) {
      const cwd = ctx.cwd;
      const summaryPath = resolvePath(cwd, params.summaryPath || "results/unity-tests-agent/latest-summary.json");
      const summary = readJsonSafe<any>(summaryPath);
      if (!summary) {
        return {
          content: [{ type: "text", text: `Summary file not found or unreadable: ${summaryPath}` }],
          isError: true,
        };
      }

      const failures = flattenFailures(summary, Math.max(1, Math.floor(params.maxFailures || 8)));
      if (failures.length === 0) {
        return {
          content: [{ type: "text", text: "No failures found in summary. Nothing to bugsplat." }],
          details: { summaryPath },
        };
      }

      const analysis = await runBugsplatSubagent(pi, cwd, summaryPath, summary, failures);
      return {
        content: [{ type: "text", text: analysis }],
        details: { summaryPath, failures },
      };
    },
  });
}
