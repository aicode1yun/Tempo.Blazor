// src/Tempo.Blazor/wwwroot/js/reporting/reporting-painter.mjs
var DEFAULT_PIXEL_RATIO = 1;
async function loadReportingFonts(fontFaces = []) {
  const faces = Array.isArray(fontFaces) ? fontFaces : [];
  if (faces.length === 0 || !globalThis.document?.fonts) {
    return [];
  }
  const urlFaces = faces.filter((face) => !!fontFaceUrl(face));
  for (const face of urlFaces) {
    const url = fontFaceUrl(face);
    const family = fontFaceFamily(face);
    const weight = fontFaceWeight(face);
    const fontStyle = fontFaceStyle(face);
    const response = await fetch(url);
    if (!response.ok) {
      throw new Error(`Could not load reporting font '${family}' from ${url}: ${response.status}`);
    }
    const bytes = await response.arrayBuffer();
    const font = new FontFace(family, bytes, {
      style: fontStyle,
      weight,
      display: "block"
    });
    await font.load();
    document.fonts.add(font);
  }
  const dataFaces = faces.filter((face) => !fontFaceUrl(face));
  if (dataFaces.length > 0) {
    const style = document.createElement("style");
    style.setAttribute("data-tempo-reporting-fonts", "f0");
    style.textContent = dataFaces.map((face) => {
      const family = cssString(fontFaceFamily(face));
      const weight = fontFaceWeight(face);
      const fontStyle = fontFaceStyle(face);
      const format = getProperty(face, "format", "Format") || "truetype";
      const data = getProperty(face, "base64", "Base64", "data", "Data") || "";
      return `
@font-face {
    font-family: ${family};
    font-style: ${fontStyle};
    font-weight: ${weight};
    font-display: block;
    src: url("data:font/${format};base64,${data}") format("${format}");
}`;
    }).join("\n");
    document.head.appendChild(style);
  }
  await Promise.all(faces.map((face) => {
    const family = fontFaceFamily(face);
    const weight = fontFaceWeight(face);
    const fontStyle = fontFaceStyle(face);
    const fontSize = Number(getProperty(face, "fontSize", "FontSize") || 16) || 16;
    return document.fonts.load(`${fontStyle} ${weight} ${fontSize}px ${cssFontFamily(family)}`);
  }));
  await document.fonts.ready;
  return faces.map(fontFaceFamily);
}
async function paintReportingSnapshot(canvas, snapshot, options = {}) {
  if (!canvas || typeof canvas.getContext !== "function") {
    throw new Error("A canvas is required for the reporting painter.");
  }
  const normalized = normalizeSnapshot(snapshot);
  const page = normalized.pages[0] || { width: 1, height: 1, commands: [] };
  const pixelRatio = Math.max(DEFAULT_PIXEL_RATIO, Number(options.pixelRatio || globalThis.devicePixelRatio || DEFAULT_PIXEL_RATIO) || DEFAULT_PIXEL_RATIO);
  const context = canvas.getContext("2d");
  const width = Math.max(1, Number(page.width || 1) || 1);
  const height = Math.max(1, Number(page.height || 1) || 1);
  canvas.style.width = `${width}px`;
  canvas.style.height = `${height}px`;
  canvas.width = Math.ceil(width * pixelRatio);
  canvas.height = Math.ceil(height * pixelRatio);
  context.setTransform(pixelRatio, 0, 0, pixelRatio, 0, 0);
  context.clearRect(0, 0, width, height);
  const summary = {
    pageCount: normalized.pages.length,
    commandCount: 0,
    paintedCommandCount: 0,
    textRunCount: 0,
    textRects: [],
    pixelRatio,
    width,
    height
  };
  for (const command of page.commands) {
    summary.commandCount++;
    if (await paintCommand(context, command, summary)) {
      summary.paintedCommandCount++;
    }
  }
  return summary;
}
async function measureReportingSamples(samples = []) {
  if (globalThis.document?.fonts) {
    await document.fonts.ready;
  }
  const canvas = document.createElement("canvas");
  const context = canvas.getContext("2d");
  return (Array.isArray(samples) ? samples : []).map((sample) => {
    const text = String(getProperty(sample, "text", "Text") || "");
    const letterSpacing = Number(getProperty(sample, "letterSpacing", "LetterSpacing") || 0) || 0;
    const fontSize = Number(getProperty(sample, "fontSize", "FontSize") || 12) || 12;
    const bold = Boolean(getProperty(sample, "bold", "Bold"));
    const italic = Boolean(getProperty(sample, "italic", "Italic"));
    const fontWeight = getProperty(sample, "fontWeight", "FontWeight") || (bold ? "700" : "400");
    const fontStyle = getProperty(sample, "fontStyle", "FontStyle") || (italic ? "italic" : "normal");
    context.font = `${fontStyle} ${fontWeight} ${fontSize}px ${cssFontFamily(getProperty(sample, "fontFamily", "FontFamily") || "sans-serif")}`;
    context.textBaseline = "alphabetic";
    if ("fontKerning" in context) {
      context.fontKerning = getProperty(sample, "kerning", "Kerning") === false ? "none" : "normal";
    }
    const naturalWidth = Number(context.measureText(text).width) || 0;
    const width = naturalWidth + Math.max(0, Array.from(text).length - 1) * letterSpacing;
    return {
      id: getProperty(sample, "id", "Id") || "",
      text,
      width,
      naturalWidth,
      font: context.font
    };
  });
}
function normalizeSnapshot(snapshot) {
  const source = snapshot || {};
  return {
    schemaVersion: Number(source.schemaVersion || source.SchemaVersion || 1) || 1,
    pages: Array.isArray(source.pages || source.Pages) ? (source.pages || source.Pages).map((page) => ({
      pageNumber: Number(page.pageNumber || page.PageNumber || 1) || 1,
      width: Number(page.width || page.Width || 1) || 1,
      height: Number(page.height || page.Height || 1) || 1,
      commands: Array.isArray(page.commands || page.Commands) ? (page.commands || page.Commands).map(normalizeCommand) : []
    })) : []
  };
}
function normalizeCommand(command) {
  const type = String(command.type || command.Type || "").trim();
  return {
    id: command.id || command.Id || "",
    type: type.charAt(0).toLowerCase() + type.slice(1),
    x: Number(command.x ?? command.X ?? 0) || 0,
    y: Number(command.y ?? command.Y ?? 0) || 0,
    width: Number(command.width ?? command.Width ?? 0) || 0,
    height: Number(command.height ?? command.Height ?? 0) || 0,
    text: command.text ?? command.Text ?? "",
    baseline: Number(command.baseline ?? command.Baseline ?? command.y ?? command.Y ?? 0) || 0,
    fontFamily: command.fontFamily || command.FontFamily || "sans-serif",
    fontSize: Number(command.fontSize ?? command.FontSize ?? 12) || 12,
    fontWeight: command.fontWeight || command.FontWeight || "400",
    fontStyle: command.fontStyle || command.FontStyle || "normal",
    letterSpacing: Number(command.letterSpacing ?? command.LetterSpacing ?? 0) || 0,
    fill: command.fill || command.Fill || "",
    stroke: command.stroke || command.Stroke || "",
    strokeWidth: Number(command.strokeWidth ?? command.StrokeWidth ?? 1) || 1,
    pathData: command.pathData || command.PathData || "",
    source: command.source || command.Source || "",
    rotation: Number(command.rotation ?? command.Rotation ?? 0) || 0
  };
}
async function paintCommand(context, command, summary) {
  switch (command.type) {
    case "rectangle":
      paintRectangle(context, command);
      return true;
    case "line":
      paintLine(context, command);
      return true;
    case "image":
      return paintImage(context, command);
    case "textRun":
      paintTextRun(context, command, summary);
      return true;
    case "path":
      return paintPath(context, command);
    case "clipPush":
      context.save();
      context.beginPath();
      context.rect(command.x, command.y, Math.max(0, command.width), Math.max(0, command.height));
      context.clip();
      return true;
    case "clipPop":
      context.restore();
      return true;
    default:
      return false;
  }
}
function paintRectangle(context, command) {
  context.save();
  if (command.fill) {
    context.fillStyle = command.fill;
    context.fillRect(command.x, command.y, command.width, command.height);
  }
  if (command.stroke && command.strokeWidth > 0) {
    context.strokeStyle = command.stroke;
    context.lineWidth = command.strokeWidth;
    context.strokeRect(command.x, command.y, command.width, command.height);
  }
  context.restore();
}
function paintLine(context, command) {
  context.save();
  context.strokeStyle = command.stroke || command.fill || "#111827";
  context.lineWidth = Math.max(0.5, command.strokeWidth || 1);
  context.beginPath();
  context.moveTo(command.x, command.y);
  context.lineTo(command.x + command.width, command.y + command.height);
  context.stroke();
  context.restore();
}
async function paintImage(context, command) {
  if (!command.source) {
    return false;
  }
  const image = new Image();
  const loaded = new Promise((resolve) => {
    image.onload = () => resolve(true);
    image.onerror = () => resolve(false);
  });
  image.decoding = "async";
  image.src = command.source;
  if (!image.complete && !await loaded) {
    return false;
  }
  try {
    if (typeof image.decode === "function") {
      await image.decode();
    }
  } catch {
    if (!image.complete) {
      return false;
    }
  }
  context.save();
  context.drawImage(image, command.x, command.y, command.width, command.height);
  context.restore();
  return true;
}
function paintPath(context, command) {
  if (!command.pathData || typeof Path2D !== "function") {
    return false;
  }
  context.save();
  const path = new Path2D(command.pathData);
  if (command.fill) {
    context.fillStyle = command.fill;
    context.fill(path);
  }
  if (command.stroke && command.strokeWidth > 0) {
    context.strokeStyle = command.stroke;
    context.lineWidth = command.strokeWidth;
    context.stroke(path);
  }
  context.restore();
  return true;
}
function paintTextRun(context, command, summary) {
  const text = String(command.text || "");
  const width = Math.max(0, Number(command.width || 0) || 0);
  const height = Math.max(1, Number(command.height || command.fontSize * 1.25) || 1);
  const baseline = Number(command.baseline || command.y + height * 0.8) || 0;
  const font = `${command.fontStyle || "normal"} ${command.fontWeight || "400"} ${command.fontSize}px ${cssFontFamily(command.fontFamily)}`;
  const letterSpacing = Number(command.letterSpacing || 0) || 0;
  const rotation = Number(command.rotation || 0) || 0;
  context.save();
  context.font = font;
  context.textBaseline = "alphabetic";
  if ("fontKerning" in context) {
    context.fontKerning = "none";
  }
  const naturalWidth = (Number(context.measureText(text).width) || 0) + Math.max(0, Array.from(text).length - 1) * letterSpacing;
  const scaleX = naturalWidth > 0 && width > 0 ? width / naturalWidth : 1;
  context.fillStyle = command.fill || "#111827";
  context.translate(command.x, baseline);
  if (Math.abs(rotation) > 1e-4) {
    context.rotate(rotation * Math.PI / 180);
  }
  context.scale(scaleX, 1);
  fillTextWithLetterSpacing(context, text, letterSpacing);
  context.restore();
  summary.textRunCount++;
  summary.textRects.push({
    id: command.id,
    text,
    x: command.x,
    y: baseline - height,
    width,
    height,
    baseline,
    naturalWidth,
    scaleX,
    rotation
  });
}
function fillTextWithLetterSpacing(context, text, letterSpacing) {
  if (Math.abs(letterSpacing) < 1e-4) {
    context.fillText(text, 0, 0);
    return;
  }
  let cursor = 0;
  const glyphs = Array.from(text);
  glyphs.forEach((glyph, index) => {
    context.fillText(glyph, cursor, 0);
    cursor += Number(context.measureText(glyph).width) || 0;
    if (index < glyphs.length - 1) {
      cursor += letterSpacing;
    }
  });
}
function cssFontFamily(value) {
  const family = String(value || "sans-serif");
  if (family.includes(",") || /^["'].*["']$/.test(family)) {
    return family;
  }
  return cssString(family);
}
function cssString(value) {
  return `"${String(value).replaceAll("\\", "\\\\").replaceAll('"', '\\"')}"`;
}
function getProperty(source, ...names) {
  for (const name of names) {
    if (Object.prototype.hasOwnProperty.call(source || {}, name) && source[name] !== void 0 && source[name] !== null) {
      return source[name];
    }
  }
  return void 0;
}
function fontFaceUrl(face) {
  return String(getProperty(face, "url", "Url") || "");
}
function fontFaceFamily(face) {
  return String(getProperty(face, "family", "Family", "fontFamily", "FontFamily") || "Tempo Reporting Font");
}
function fontFaceWeight(face) {
  return String(getProperty(face, "weight", "Weight", "fontWeight", "FontWeight") || "400");
}
function fontFaceStyle(face) {
  return String(getProperty(face, "style", "Style", "fontStyle", "FontStyle") || "normal");
}
export {
  loadReportingFonts,
  measureReportingSamples,
  paintReportingSnapshot
};
