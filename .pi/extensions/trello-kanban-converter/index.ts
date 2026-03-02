import type { ExtensionAPI } from "@mariozechner/pi-coding-agent";
import { Type } from "@sinclair/typebox";

import * as fs from "node:fs/promises";
import * as path from "node:path";
import crypto from "node:crypto";

type TrelloList = {
  id: string;
  name: string;
  closed?: boolean;
  pos?: number;
};

type TrelloCard = {
  id: string;
  name: string;
  desc?: string;
  closed?: boolean;
  idList: string;
  idLabels?: string[];
  labels?: Array<{ id?: string; name?: string; color?: string }>;
  pos?: number;
  due?: string | null;
  dueComplete?: boolean;
  shortLink?: string;
  url?: string;
};

type TrelloChecklist = {
  id: string;
  idCard: string;
  name: string;
  checkItems: Array<{
    id: string;
    name: string;
    state: "complete" | "incomplete";
  }>;
};

type TrelloLabel = {
  id: string;
  name: string;
  color?: string | null;
};

type TrelloExport = {
  name?: string;
  lists?: TrelloList[];
  cards?: TrelloCard[];
  checklists?: TrelloChecklist[];
  labels?: TrelloLabel[];
};

function stripAtPrefix(p: string) {
  return p.startsWith("@") ? p.slice(1) : p;
}

function resolveCwd(cwd: string, p: string) {
  const pp = stripAtPrefix(p);
  return path.isAbsolute(pp) ? pp : path.join(cwd, pp);
}

function safeId(prefix: string) {
  return `${prefix}_${crypto.randomBytes(8).toString("hex")}`;
}

function sortByPos<T extends { pos?: number }>(arr: T[]) {
  return [...arr].sort((a, b) => (a.pos ?? 0) - (b.pos ?? 0));
}

function normalizeLabelToTag(label: string) {
  // Obsidian tags can't contain spaces. Keep it conservative.
  return label
    .trim()
    .replace(/^#+/, "")
    .replace(/\s+/g, "-")
    .replace(/[^a-zA-Z0-9_\-/]/g, "")
    .slice(0, 64);
}

function extractTagsFromTitle(title: string): { title: string; tags: string[] } {
  const parts = title.split(/\s+/);
  const tags: string[] = [];

  // Treat trailing #tags as tags (common convention)
  while (parts.length > 0 && parts[parts.length - 1].startsWith("#")) {
    const raw = parts.pop()!;
    const tag = normalizeLabelToTag(raw);
    if (tag) tags.push(tag);
  }

  return { title: parts.join(" ").trim(), tags: tags.reverse() };
}

function buildKanbanSettings(listCount: number) {
  return {
    "kanban-plugin": "board",
    "list-collapse": Array.from({ length: Math.max(0, listCount) }, () => false),
  };
}

function kanbanBoardMarkdown(params: {
  boardName?: string;
  lists: Array<{
    name: string;
    cards: Array<{
      checkChar: " " | "x";
      title: string;
      tags?: string[];
      checklistItems?: Array<{ checkChar: " " | "x"; title: string }>;
    }>;
  }>;
  archiveCards?: Array<{
    checkChar: " " | "x";
    title: string;
    tags?: string[];
    checklistItems?: Array<{ checkChar: " " | "x"; title: string }>;
  }>;
}) {
  const frontmatterLines = ["---", "kanban-plugin: board"];
  if (params.boardName) {
    // purely informational; Kanban plugin ignores this
    frontmatterLines.push(`title: ${JSON.stringify(params.boardName)}`);
  }
  frontmatterLines.push("---", "");

  const settings = buildKanbanSettings(params.lists.length);

  const out: string[] = [];
  out.push(frontmatterLines.join("\n"));

  for (const list of params.lists) {
    out.push(`## ${list.name}`);
    out.push("");

    for (const card of list.cards) {
      const tagSuffix = (card.tags ?? []).length ? " " + (card.tags ?? []).map((t) => `#${t}`).join(" ") : "";
      out.push(`- [${card.checkChar}] ${card.title}${tagSuffix}`);
      if (card.checklistItems && card.checklistItems.length) {
        for (const ci of card.checklistItems) {
          out.push(`    - [${ci.checkChar}] ${ci.title}`);
        }
      }
    }

    // plugin writes multiple blank lines after each list
    out.push("", "", "");
  }

  if (params.archiveCards && params.archiveCards.length) {
    out.push("***", "", "## Archive", "");
    for (const card of params.archiveCards) {
      const tagSuffix = (card.tags ?? []).length ? " " + (card.tags ?? []).map((t) => `#${t}`).join(" ") : "";
      out.push(`- [${card.checkChar}] ${card.title}${tagSuffix}`);
      if (card.checklistItems && card.checklistItems.length) {
        for (const ci of card.checklistItems) {
          out.push(`    - [${ci.checkChar}] ${ci.title}`);
        }
      }
    }
  }

  // settings block at end (as used by obsidian-kanban)
  out.push("", "", "%% kanban:settings", "```", JSON.stringify(settings), "```", "%%", "");

  return out.join("\n");
}

function parseTrelloExport(json: unknown): TrelloExport {
  if (!json || typeof json !== "object") throw new Error("Not a JSON object");
  return json as TrelloExport;
}

function parseKanbanMarkdown(md: string) {
  const lines = md.replace(/\r\n/g, "\n").split("\n");

  // Strip YAML frontmatter if present
  let i = 0;
  if (lines[i]?.trim() === "---") {
    i++;
    while (i < lines.length && lines[i]?.trim() !== "---") i++;
    if (lines[i]?.trim() === "---") i++;
  }

  type ParsedCard = {
    checkChar: " " | "x";
    title: string;
    tags: string[];
    checklistItems: Array<{ checkChar: " " | "x"; title: string }>;
  };

  const lists: Array<{ name: string; cards: ParsedCard[] }> = [];
  let currentList: { name: string; cards: ParsedCard[] } | null = null;
  let currentCard: ParsedCard | null = null;
  let inSettings = false;

  const pushListIfNeeded = () => {
    if (currentList && !lists.includes(currentList)) lists.push(currentList);
  };

  for (; i < lines.length; i++) {
    const raw = lines[i];
    const line = raw.trimEnd();

    if (line.trim() === "%% kanban:settings") {
      inSettings = true;
      continue;
    }
    if (inSettings) continue;

    const heading = /^##\s+(.*)$/.exec(line);
    if (heading) {
      pushListIfNeeded();
      currentList = { name: heading[1].trim(), cards: [] };
      currentCard = null;
      continue;
    }

    // Ignore separators like ***
    if (line.trim() === "***") {
      currentCard = null;
      continue;
    }

    const cardMatch = /^- \[([ xX])\] (.*)$/.exec(line.trim());
    if (cardMatch && currentList) {
      const checkChar = cardMatch[1].toLowerCase() === "x" ? "x" : " ";
      const { title, tags } = extractTagsFromTitle(cardMatch[2]);
      currentCard = { checkChar, title, tags, checklistItems: [] };
      currentList.cards.push(currentCard);
      continue;
    }

    const subMatch = /^\s{4}- \[([ xX])\] (.*)$/.exec(raw);
    if (subMatch && currentCard) {
      const checkChar = subMatch[1].toLowerCase() === "x" ? "x" : " ";
      const title = subMatch[2].trim();
      currentCard.checklistItems.push({ checkChar, title });
      continue;
    }
  }
  pushListIfNeeded();

  return { lists };
}

export default function (pi: ExtensionAPI) {
  pi.registerTool({
    name: "trello_to_kanban",
    label: "Trello → Obsidian Kanban",
    description:
      "Convert a Trello JSON export into an obsidian-kanban board markdown file (project format).\n\nOutput format: YAML frontmatter with kanban-plugin: board, lists as ## headings, cards as - [ ] tasks, optional Archive section, and a %% kanban:settings block.",
    parameters: Type.Object({
      inputJsonPath: Type.String({ description: "Path to Trello board export JSON" }),
      outputMdPath: Type.String({ description: "Path to output .md kanban board" }),
      includeArchived: Type.Optional(
        Type.Boolean({
          description:
            "If true, include archived/closed cards and cards from closed lists under an Archive section.",
          default: true,
        })
      ),
      includeChecklistItems: Type.Optional(
        Type.Boolean({
          description: "If true, convert Trello checklists into nested checkbox items.",
          default: true,
        })
      ),
      includeLabelsAsTags: Type.Optional(
        Type.Boolean({
          description: "If true, convert Trello labels to #tags appended to card titles.",
          default: true,
        })
      ),
    }),
    async execute(_toolCallId, params, _signal, _onUpdate, ctx) {
      const inputPath = resolveCwd(ctx.cwd, params.inputJsonPath);
      const outputPath = resolveCwd(ctx.cwd, params.outputMdPath);

      // TypeBox defaults are not automatically applied at runtime.
      const includeArchived = params.includeArchived !== false;
      const includeChecklistItems = params.includeChecklistItems !== false;
      const includeLabelsAsTags = params.includeLabelsAsTags !== false;

      const raw = await fs.readFile(inputPath, "utf-8");
      const trello = parseTrelloExport(JSON.parse(raw));

      const lists = sortByPos((trello.lists ?? []).filter((l) => !l.closed));
      const closedLists = new Set((trello.lists ?? []).filter((l) => l.closed).map((l) => l.id));

      const labelsById = new Map((trello.labels ?? []).map((l) => [l.id, l.name || ""] as const));

      const checklistsByCard = new Map<string, TrelloChecklist[]>();
      for (const cl of trello.checklists ?? []) {
        const arr = checklistsByCard.get(cl.idCard) ?? [];
        arr.push(cl);
        checklistsByCard.set(cl.idCard, arr);
      }

      const cards = sortByPos(trello.cards ?? []);

      const listCards = new Map<string, TrelloCard[]>();
      const archiveCards: TrelloCard[] = [];

      for (const c of cards) {
        const isArchived = !!c.closed || closedLists.has(c.idList);
        if (isArchived) {
          archiveCards.push(c);
          continue;
        }
        const arr = listCards.get(c.idList) ?? [];
        arr.push(c);
        listCards.set(c.idList, arr);
      }

      const outLists = lists.map((l) => {
        const lCards = listCards.get(l.id) ?? [];
        return {
          name: l.name || "",
          cards: lCards.map((c) => {
            const tags: string[] = [];
            if (includeLabelsAsTags) {
              const ids = c.idLabels ?? [];
              for (const id of ids) {
                const nm = labelsById.get(id) ?? "";
                const tag = normalizeLabelToTag(nm);
                if (tag) tags.push(tag);
              }
            }

            const checklistItems: Array<{ checkChar: " " | "x"; title: string }> = [];
            if (includeChecklistItems) {
              for (const cl of checklistsByCard.get(c.id) ?? []) {
                for (const item of cl.checkItems ?? []) {
                  checklistItems.push({
                    checkChar: item.state === "complete" ? "x" : " ",
                    title: item.name,
                  });
                }
              }
            }

            return {
              checkChar: " " as const,
              title: c.name,
              tags,
              checklistItems,
            };
          }),
        };
      });

      const outArchive = (includeArchived ? archiveCards : []).map((c) => {
        const tags: string[] = [];
        if (includeLabelsAsTags) {
          const ids = c.idLabels ?? [];
          for (const id of ids) {
            const nm = labelsById.get(id) ?? "";
            const tag = normalizeLabelToTag(nm);
            if (tag) tags.push(tag);
          }
        }

        const checklistItems = includeChecklistItems
          ? (checklistsByCard.get(c.id) ?? []).flatMap((cl) =>
              (cl.checkItems ?? []).map((item) => ({
                checkChar: item.state === "complete" ? ("x" as const) : (" " as const),
                title: item.name,
              }))
            )
          : [];

        return {
          checkChar: " " as const,
          title: c.name,
          tags,
          checklistItems,
        };
      });

      const md = kanbanBoardMarkdown({
        boardName: trello.name,
        lists: outLists,
        archiveCards: outArchive.length ? outArchive : undefined,
      });

      await fs.mkdir(path.dirname(outputPath), { recursive: true });
      await fs.writeFile(outputPath, md, "utf-8");

      const openCardCount = outLists.reduce((n, l) => n + l.cards.length, 0);
      const archiveCount = outArchive.length;

      return {
        content: [
          {
            type: "text",
            text:
              `Wrote kanban board: ${path.relative(ctx.cwd, outputPath)}\n` +
              `Lists: ${outLists.length}\n` +
              `Open cards: ${openCardCount}` +
              (includeArchived ? `\nArchived cards: ${archiveCount}` : ""),
          },
        ],
        details: {
          inputPath,
          outputPath,
          lists: outLists.length,
          openCards: openCardCount,
          archivedCards: archiveCount,
        },
      };
    },
  });

  pi.registerTool({
    name: "kanban_to_trello",
    label: "Obsidian Kanban → Trello JSON",
    description:
      "Convert an obsidian-kanban board markdown file into a Trello-like JSON export (best-effort).\n\nNote: This generates a *new* Trello-shaped JSON (IDs are generated). It is intended for round-tripping content, not for updating an existing Trello board.",
    parameters: Type.Object({
      inputMdPath: Type.String({ description: "Path to input .md kanban board" }),
      outputJsonPath: Type.String({ description: "Path to output JSON" }),
      boardName: Type.Optional(Type.String({ description: "Optional board name override" })),
      pretty: Type.Optional(Type.Boolean({ description: "Pretty-print JSON", default: true })),
    }),
    async execute(_toolCallId, params, _signal, _onUpdate, ctx) {
      const inputPath = resolveCwd(ctx.cwd, params.inputMdPath);
      const outputPath = resolveCwd(ctx.cwd, params.outputJsonPath);

      const md = await fs.readFile(inputPath, "utf-8");
      const parsed = parseKanbanMarkdown(md);

      const labels = new Map<string, TrelloLabel>();
      const lists: TrelloList[] = [];
      const cards: TrelloCard[] = [];
      const checklists: TrelloChecklist[] = [];

      for (const l of parsed.lists) {
        const listId = safeId("list");
        lists.push({ id: listId, name: l.name, closed: false, pos: lists.length + 1 });

        for (const c of l.cards) {
          const cardId = safeId("card");

          // tags -> labels
          const idLabels: string[] = [];
          for (const t of c.tags) {
            const key = t.toLowerCase();
            if (!labels.has(key)) {
              labels.set(key, { id: safeId("label"), name: t, color: null });
            }
            idLabels.push(labels.get(key)!.id);
          }

          const closed = c.checkChar === "x";
          cards.push({
            id: cardId,
            name: c.title,
            idList: listId,
            closed,
            desc: "",
            idLabels,
            pos: cards.length + 1,
          });

          if (c.checklistItems.length) {
            const checklistId = safeId("checklist");
            checklists.push({
              id: checklistId,
              idCard: cardId,
              name: "Checklist",
              checkItems: c.checklistItems.map((ci) => ({
                id: safeId("checkitem"),
                name: ci.title,
                state: ci.checkChar === "x" ? "complete" : "incomplete",
              })),
            });
          }
        }
      }

      const trelloLike: TrelloExport = {
        name: params.boardName ?? path.parse(inputPath).name,
        lists,
        cards,
        checklists,
        labels: Array.from(labels.values()),
      };

      await fs.mkdir(path.dirname(outputPath), { recursive: true });
      const json = params.pretty === false ? JSON.stringify(trelloLike) : JSON.stringify(trelloLike, null, 2);
      await fs.writeFile(outputPath, json, "utf-8");

      return {
        content: [
          {
            type: "text",
            text:
              `Wrote Trello-like JSON: ${path.relative(ctx.cwd, outputPath)}\n` +
              `Lists: ${lists.length}\nCards: ${cards.length}\nChecklists: ${checklists.length}\nLabels: ${labels.size}`,
          },
        ],
        details: {
          inputPath,
          outputPath,
          lists: lists.length,
          cards: cards.length,
          checklists: checklists.length,
          labels: labels.size,
        },
      };
    },
  });

  pi.registerCommand("trello-to-kanban", {
    description: "Convert Trello JSON export to an obsidian-kanban markdown board",
    handler: async (args, ctx) => {
      ctx.ui.notify(
        "Use the trello_to_kanban tool (recommended), or pass args: <input.json> <output.md>",
        "info"
      );
      if (!args) return;
      const parts = args.split(/\s+/).filter(Boolean);
      if (parts.length < 2) return;
      // Queue a tool call by sending a follow-up user message for the agent
      pi.sendUserMessage(
        `Use trello_to_kanban with inputJsonPath: ${parts[0]} and outputMdPath: ${parts[1]}.`,
        { deliverAs: "followUp" }
      );
    },
  });

  pi.registerCommand("kanban-to-trello", {
    description: "Convert an obsidian-kanban markdown board to Trello-like JSON",
    handler: async (args, ctx) => {
      ctx.ui.notify(
        "Use the kanban_to_trello tool (recommended), or pass args: <input.md> <output.json>",
        "info"
      );
      if (!args) return;
      const parts = args.split(/\s+/).filter(Boolean);
      if (parts.length < 2) return;
      pi.sendUserMessage(
        `Use kanban_to_trello with inputMdPath: ${parts[0]} and outputJsonPath: ${parts[1]}.`,
        { deliverAs: "followUp" }
      );
    },
  });
}
