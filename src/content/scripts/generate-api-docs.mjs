#!/usr/bin/env node
/**
 * Generates Markdown documentation from .NET XML documentation files
 * for use with Astro Starlight.
 */

import { readFileSync, writeFileSync, mkdirSync, existsSync } from 'fs';
import { join, dirname } from 'path';
import { fileURLToPath } from 'url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const XML_PATH = join(__dirname, '../../../src/Hex1b/bin/Debug/net10.0/Hex1b.xml');
const OUTPUT_DIR = join(__dirname, '../src/content/docs/reference');

// Simple XML parser for .NET documentation format
function parseXml(xml) {
  const members = [];
  const memberRegex = /<member name="([^"]+)">([\s\S]*?)<\/member>/g;
  let match;
  
  while ((match = memberRegex.exec(xml)) !== null) {
    const [, name, content] = match;
    members.push({ name, content: content.trim() });
  }
  
  return members;
}

// Extract text content from an XML element
function extractElement(content, tag) {
  const regex = new RegExp(`<${tag}[^>]*>([\\s\\S]*?)<\\/${tag}>`, 'i');
  const match = content.match(regex);
  return match ? cleanText(match[1]) : '';
}

// Extract all example elements (there can be multiple)
function extractExamples(content) {
  const examples = [];
  const exampleRegex = /<example>([\s\S]*?)<\/example>/gi;
  let match;
  
  while ((match = exampleRegex.exec(content)) !== null) {
    const exampleContent = match[1];
    
    // Extract description (text in <para> tags before <code>)
    const paraMatch = exampleContent.match(/<para>([^<]*)<\/para>/);
    const description = paraMatch ? cleanText(paraMatch[1]) : '';
    
    // Extract code block
    const codeMatch = exampleContent.match(/<code>([\s\S]*?)<\/code>/);
    let code = codeMatch ? codeMatch[1] : '';
    
    if (code) {
      // Clean up the code: decode HTML entities and fix indentation
      code = cleanCode(code);
      examples.push({ description, code });
    }
  }
  
  return examples;
}

// Clean up code from XML documentation
function cleanCode(code) {
  // Decode HTML entities
  code = code
    .replace(/&lt;/g, '<')
    .replace(/&gt;/g, '>')
    .replace(/&amp;/g, '&')
    .replace(/&quot;/g, '"')
    .replace(/&apos;/g, "'");
  
  // Split into lines and remove common leading whitespace
  const lines = code.split('\n');
  
  // Remove empty first/last lines
  while (lines.length > 0 && lines[0].trim() === '') lines.shift();
  while (lines.length > 0 && lines[lines.length - 1].trim() === '') lines.pop();
  
  if (lines.length === 0) return '';
  
  // Find minimum indentation (ignoring empty lines)
  const minIndent = lines
    .filter(line => line.trim().length > 0)
    .reduce((min, line) => {
      const leadingSpaces = line.match(/^(\s*)/)[1].length;
      return Math.min(min, leadingSpaces);
    }, Infinity);
  
  // Remove the common indentation
  const dedented = lines.map(line => 
    line.length >= minIndent ? line.slice(minIndent) : line
  );
  
  return dedented.join('\n');
}

// Clean up XML text content
function cleanText(text) {
  return text
    .replace(/<see cref="[^:]+:([^"]+)"[^>]*\/>/g, '`$1`') // Convert <see cref="T:Hex1b.Foo"/> to `Foo`
    .replace(/<paramref name="([^"]+)"[^>]*\/>/g, '`$1`') // Convert <paramref name="x"/> to `x`
    .replace(/<c>([^<]+)<\/c>/g, '`$1`') // Convert <c>code</c> to `code`
    .replace(/<para>/g, '\n\n')
    .replace(/<\/para>/g, '')
    .replace(/<[^>]+>/g, '') // Remove remaining XML tags
    .replace(/\s+/g, ' ')
    .trim();
}

// Parse member name like "T:Hex1b.ButtonExtensions" or "M:Hex1b.Foo.Bar(System.String)"
function parseMemberName(name) {
  const [type, fullName] = [name[0], name.substring(2)];
  
  // Find the method name - it's after the last dot before any parentheses
  // But we need to handle generic methods like "Border``2"
  let methodPart = fullName;
  let params = [];
  
  // Find opening paren for parameters (not inside angle brackets)
  const parenIndex = fullName.indexOf('(');
  if (parenIndex !== -1) {
    methodPart = fullName.substring(0, parenIndex);
    const paramsPart = fullName.substring(parenIndex + 1, fullName.lastIndexOf(')'));
    
    // Parse parameters, handling nested generics
    if (paramsPart) {
      params = splitParameters(paramsPart).map(p => {
        // Simplify to just the type name
        return simplifyTypeName(p);
      });
    }
  }
  
  // Split by dots to get namespace and method name
  const parts = methodPart.split('.');
  let methodName = parts[parts.length - 1];
  
  // Clean up generic type notation from method name
  methodName = methodName.replace(/``\d+/g, '').replace(/`\d+/g, '');
  
  const namespace = parts.slice(0, -1).join('.');
  const className = parts.length > 1 ? parts[parts.length - 2] : '';
  
  return {
    type, // T=Type, M=Method, P=Property, F=Field, E=Event
    fullName,
    namespace,
    className,
    name: methodName,
    params
  };
}

// Split parameter list handling nested generic types
function splitParameters(paramsPart) {
  const result = [];
  let current = '';
  let depth = 0;
  
  for (const char of paramsPart) {
    if (char === '{') depth++;
    else if (char === '}') depth--;
    else if (char === ',' && depth === 0) {
      result.push(current.trim());
      current = '';
      continue;
    }
    current += char;
  }
  
  if (current.trim()) {
    result.push(current.trim());
  }
  
  return result;
}

// Simplify a .NET type name for display
function simplifyTypeName(typeName) {
  // Handle generic parameter references like ``0, ``1, `0, `1
  typeName = typeName.replace(/``(\d+)/g, (_, n) => `T${parseInt(n) + 1}`);
  typeName = typeName.replace(/`(\d+)/g, (_, n) => `T${parseInt(n) + 1}`);
  
  // Extract just the type name from fully qualified names
  // Handle generics like System.Func{System.String,System.Int32}
  const simplified = typeName
    .replace(/\{[^}]+\}/g, '') // Remove generic arguments for display
    .split('.')
    .pop() || typeName;
  
  // Common type simplifications
  const typeMap = {
    'String': 'string',
    'Int32': 'int',
    'Int64': 'long',
    'Boolean': 'bool',
    'Double': 'double',
    'Single': 'float',
    'Void': 'void',
    'Object': 'object',
  };
  
  return typeMap[simplified] || simplified;
}

// Group members by class
function groupByClass(members) {
  const classes = new Map();
  
  for (const member of members) {
    const parsed = parseMemberName(member.name);
    
    // Skip internal/private members (those with weird names)
    if (parsed.name.startsWith('<') || parsed.name.includes('__')) continue;
    
    // Determine the class this belongs to
    let className;
    if (parsed.type === 'T') {
      className = parsed.fullName;
    } else {
      // For methods/properties, the class is everything before the last dot (before params)
      const withoutParams = parsed.fullName.replace(/\([^)]*\)$/, '');
      const lastDot = withoutParams.lastIndexOf('.');
      className = lastDot > 0 ? withoutParams.substring(0, lastDot) : withoutParams;
    }
    
    if (!classes.has(className)) {
      classes.set(className, {
        name: className,
        summary: '',
        remarks: '',
        examples: [],
        methods: [],
        properties: [],
        fields: [],
        events: []
      });
    }
    
    const classInfo = classes.get(className);
    const summary = extractElement(member.content, 'summary');
    const remarks = extractElement(member.content, 'remarks');
    const returns = extractElement(member.content, 'returns');
    const examples = extractExamples(member.content);
    
    // Extract parameters
    const paramRegex = /<param name="([^"]+)">([^<]*)<\/param>/g;
    const parameters = [];
    let paramMatch;
    while ((paramMatch = paramRegex.exec(member.content)) !== null) {
      parameters.push({ name: paramMatch[1], description: cleanText(paramMatch[2]) });
    }
    
    if (parsed.type === 'T') {
      classInfo.summary = summary;
      classInfo.remarks = remarks;
      classInfo.examples = examples;
    } else if (parsed.type === 'M') {
      classInfo.methods.push({
        name: parsed.name,
        params: parsed.params,
        summary,
        returns,
        parameters
      });
    } else if (parsed.type === 'P') {
      classInfo.properties.push({ name: parsed.name, summary });
    } else if (parsed.type === 'F') {
      classInfo.fields.push({ name: parsed.name, summary });
    } else if (parsed.type === 'E') {
      classInfo.events.push({ name: parsed.name, summary });
    }
  }
  
  return classes;
}

// Categorize classes by their purpose
function categorizeClasses(classes) {
  const categories = {
    widgets: [],
    nodes: [],
    layout: [],
    input: [],
    theming: [],
    extensions: [],
    core: []
  };
  
  for (const [name, info] of classes) {
    const shortName = name.split('.').pop();
    
    if (name.includes('.Widgets.') || shortName.endsWith('Widget')) {
      categories.widgets.push({ name, shortName, info });
    } else if (name.includes('.Nodes.') || shortName.endsWith('Node')) {
      categories.nodes.push({ name, shortName, info });
    } else if (name.includes('.Layout.')) {
      categories.layout.push({ name, shortName, info });
    } else if (name.includes('.Input.') || shortName.includes('Input') || shortName.includes('Key')) {
      categories.input.push({ name, shortName, info });
    } else if (name.includes('.Theming.')) {
      categories.theming.push({ name, shortName, info });
    } else if (shortName.endsWith('Extensions')) {
      categories.extensions.push({ name, shortName, info });
    } else {
      categories.core.push({ name, shortName, info });
    }
  }
  
  return categories;
}

// Escape a string for YAML frontmatter
function escapeYaml(str) {
  if (!str) return '';
  // If contains special characters, wrap in quotes and escape internal quotes
  if (str.includes(':') || str.includes('#') || str.includes("'") || str.includes('"') || str.includes('\n')) {
    return `"${str.replace(/"/g, '\\"').replace(/\n/g, ' ')}"`;
  }
  return str;
}

// Truncate description for frontmatter (keep it short)
function truncateDescription(str, maxLen = 150) {
  if (!str) return '';
  const cleaned = str.replace(/\n/g, ' ').trim();
  if (cleaned.length <= maxLen) return cleaned;
  return cleaned.substring(0, maxLen - 3) + '...';
}

// Generate Markdown for a class
function generateClassMarkdown(shortName, info, category) {
  const lines = [];
  
  // Frontmatter
  const description = truncateDescription(info.summary) || `API reference for ${shortName}`;
  lines.push('---');
  lines.push(`title: ${shortName}`);
  lines.push(`description: ${escapeYaml(description)}`);
  lines.push('---');
  lines.push('');
  
  // Summary
  if (info.summary) {
    lines.push(info.summary);
    lines.push('');
  }
  
  // Remarks
  if (info.remarks) {
    lines.push('## Remarks');
    lines.push('');
    lines.push(info.remarks);
    lines.push('');
  }
  
  // Examples
  if (info.examples && info.examples.length > 0) {
    lines.push('## Examples');
    lines.push('');
    
    for (const example of info.examples) {
      if (example.description) {
        lines.push(example.description);
        lines.push('');
      }
      
      lines.push('```csharp');
      lines.push(example.code);
      lines.push('```');
      lines.push('');
    }
  }
  
  // Properties
  if (info.properties.length > 0) {
    lines.push('## Properties');
    lines.push('');
    lines.push('| Property | Description |');
    lines.push('|----------|-------------|');
    for (const prop of info.properties) {
      lines.push(`| \`${prop.name}\` | ${prop.summary} |`);
    }
    lines.push('');
  }
  
  // Methods
  if (info.methods.length > 0) {
    lines.push('## Methods');
    lines.push('');
    
    // Group overloads
    const methodGroups = new Map();
    for (const method of info.methods) {
      if (!methodGroups.has(method.name)) {
        methodGroups.set(method.name, []);
      }
      methodGroups.get(method.name).push(method);
    }
    
    for (const [methodName, overloads] of methodGroups) {
      lines.push(`### ${methodName}`);
      lines.push('');
      
      for (const method of overloads) {
        const signature = method.params.length > 0 
          ? `${methodName}(${method.params.join(', ')})`
          : `${methodName}()`;
        
        lines.push(`#### \`${signature}\``);
        lines.push('');
        
        if (method.summary) {
          lines.push(method.summary);
          lines.push('');
        }
        
        if (method.parameters.length > 0) {
          lines.push('**Parameters:**');
          lines.push('');
          for (const param of method.parameters) {
            lines.push(`- \`${param.name}\`: ${param.description}`);
          }
          lines.push('');
        }
        
        if (method.returns) {
          lines.push(`**Returns:** ${method.returns}`);
          lines.push('');
        }
      }
    }
  }
  
  // Fields
  if (info.fields.length > 0) {
    lines.push('## Fields');
    lines.push('');
    lines.push('| Field | Description |');
    lines.push('|-------|-------------|');
    for (const field of info.fields) {
      lines.push(`| \`${field.name}\` | ${field.summary} |`);
    }
    lines.push('');
  }
  
  // Events
  if (info.events.length > 0) {
    lines.push('## Events');
    lines.push('');
    lines.push('| Event | Description |');
    lines.push('|-------|-------------|');
    for (const event of info.events) {
      lines.push(`| \`${event.name}\` | ${event.summary} |`);
    }
    lines.push('');
  }
  
  return lines.join('\n');
}

// Generate category index
function generateCategoryIndex(category, items, title, description) {
  const lines = [];
  
  lines.push('---');
  lines.push(`title: ${title}`);
  lines.push(`description: ${escapeYaml(description)}`);
  lines.push('---');
  lines.push('');
  lines.push(`# ${title}`);
  lines.push('');
  lines.push(description);
  lines.push('');
  
  if (items.length > 0) {
    lines.push('| Type | Description |');
    lines.push('|------|-------------|');
    for (const item of items.sort((a, b) => a.shortName.localeCompare(b.shortName))) {
      const link = `./${item.shortName.toLowerCase()}/`;
      lines.push(`| [\`${item.shortName}\`](${link}) | ${item.info.summary || ''} |`);
    }
  } else {
    lines.push('*No documented types in this category.*');
  }
  
  return lines.join('\n');
}

// Main
function main() {
  console.log('📖 Generating API documentation from XML...');
  
  // Check if XML file exists
  if (!existsSync(XML_PATH)) {
    console.error(`❌ XML file not found: ${XML_PATH}`);
    console.error('   Run "dotnet build src/Hex1b/Hex1b.csproj" first.');
    process.exit(1);
  }
  
  // Parse XML
  const xml = readFileSync(XML_PATH, 'utf-8');
  const members = parseXml(xml);
  console.log(`   Found ${members.length} documented members`);
  
  // Group by class
  const classes = groupByClass(members);
  console.log(`   Found ${classes.size} types`);
  
  // Categorize
  const categories = categorizeClasses(classes);
  
  // Create output directory
  if (!existsSync(OUTPUT_DIR)) {
    mkdirSync(OUTPUT_DIR, { recursive: true });
  }
  
  // Generate files for each category
  const categoryMeta = {
    widgets: { title: 'Widgets', description: 'Widget types define the declarative UI structure.' },
    nodes: { title: 'Nodes', description: 'Node types manage state, handle input, and render widgets.' },
    layout: { title: 'Layout', description: 'Layout types for measuring and arranging UI elements.' },
    input: { title: 'Input', description: 'Types for handling keyboard and mouse input.' },
    theming: { title: 'Theming', description: 'Types for styling and theming the UI.' },
    extensions: { title: 'Extensions', description: 'Extension methods for building widgets fluently.' },
    core: { title: 'Core', description: 'Core types and utilities.' }
  };
  
  let totalFiles = 0;
  
  for (const [category, items] of Object.entries(categories)) {
    if (items.length === 0) continue;
    
    const categoryDir = join(OUTPUT_DIR, category);
    if (!existsSync(categoryDir)) {
      mkdirSync(categoryDir, { recursive: true });
    }
    
    // Generate index for category
    const meta = categoryMeta[category];
    const indexContent = generateCategoryIndex(category, items, meta.title, meta.description);
    writeFileSync(join(categoryDir, 'index.md'), indexContent);
    totalFiles++;
    
    // Generate file for each type
    for (const { shortName, info } of items) {
      const content = generateClassMarkdown(shortName, info, category);
      const filename = join(categoryDir, `${shortName.toLowerCase()}.md`);
      writeFileSync(filename, content);
      totalFiles++;
    }
  }
  
  // Generate main reference index
  const mainIndex = `---
title: API Reference
description: Complete API reference for Hex1b
---

# API Reference

Complete API documentation for the Hex1b library.

## Categories

| Category | Description |
|----------|-------------|
| [Widgets](./widgets/) | Widget types define the declarative UI structure |
| [Nodes](./nodes/) | Node types manage state, handle input, and render |
| [Layout](./layout/) | Layout types for measuring and arranging elements |
| [Input](./input/) | Types for handling keyboard and mouse input |
| [Theming](./theming/) | Types for styling and theming |
| [Extensions](./extensions/) | Fluent extension methods for building widgets |
| [Core](./core/) | Core types and utilities |
`;
  
  writeFileSync(join(OUTPUT_DIR, 'overview.md'), mainIndex);
  totalFiles++;
  
  console.log(`✅ Generated ${totalFiles} documentation files`);
}

main();
