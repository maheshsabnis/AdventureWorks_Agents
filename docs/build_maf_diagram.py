"""Create editable PowerPoint diagrams for the AdventureWorks MAF article."""
from pathlib import Path
from pptx import Presentation
from pptx.util import Inches, Pt
from pptx.dml.color import RGBColor
from pptx.enum.shapes import MSO_SHAPE
from pptx.enum.text import PP_ALIGN, MSO_ANCHOR

ROOT = Path(__file__).resolve().parents[1]
prs = Presentation()
prs.slide_width = Inches(16)
prs.slide_height = Inches(9)
prs.core_properties.title = 'AdventureWorks Agent: implementation and follow-up conversation'
prs.core_properties.subject = 'Microsoft Agent Framework, ASP.NET Core 10, and Blazor WebAssembly'
prs.core_properties.author = 'AdventureWorks tutorial'
NAVY = '17324D'
BLUE = 'E5EFFB'
GREEN = 'E3F3EB'
ORANGE = 'FFF0D6'
GRAY = 'EDF0F4'


def text(slide, x, y, w, h, content, size=18, bold=False, color=NAVY, align=PP_ALIGN.LEFT):
    shape = slide.shapes.add_textbox(Inches(x), Inches(y), Inches(w), Inches(h))
    frame = shape.text_frame
    frame.word_wrap = True
    for i, line in enumerate(content.split('\n')):
        p = frame.paragraphs[0] if i == 0 else frame.add_paragraph()
        p.text = line
        p.font.name = 'Aptos'
        p.font.size = Pt(size)
        p.font.bold = bold
        p.font.color.rgb = RGBColor.from_string(color)
        p.alignment = align
        p.space_after = Pt(5)
    return shape


def box(slide, x, y, w, h, title, body, fill=BLUE):
    shape = slide.shapes.add_shape(MSO_SHAPE.ROUNDED_RECTANGLE, Inches(x), Inches(y), Inches(w), Inches(h))
    shape.fill.solid()
    shape.fill.fore_color.rgb = RGBColor.from_string(fill)
    shape.line.color.rgb = RGBColor.from_string('BCCDDD')
    text(slide, x + .12, y + .09, w - .24, .45, title, 18, True)
    text(slide, x + .12, y + .59, w - .24, h - .66, body, 15)
    return shape


def arrow(slide, x, y, w=.45, h=.3, direction='right', color='3C6E9F'):
    kind = {'right': MSO_SHAPE.RIGHT_ARROW, 'left': MSO_SHAPE.LEFT_ARROW, 'down': MSO_SHAPE.DOWN_ARROW}[direction]
    shape = slide.shapes.add_shape(kind, Inches(x), Inches(y), Inches(w), Inches(h))
    shape.fill.solid()
    shape.fill.fore_color.rgb = RGBColor.from_string(color)
    shape.line.fill.background()
    return shape


slide = prs.slides.add_slide(prs.slide_layouts[6])
text(slide, .45, .22, 15.1, .6, 'AdventureWorks AI Agent — implementation architecture', 29, True)
text(slide, .45, .9, 15.1, .5, 'One constrained agent • 22 predefined SELECT tools • No arbitrary SQL • .NET 10', 18)
box(slide, .45, 1.65, 3.15, 2.45, '1  Blazor WebAssembly', 'AgentChat\nPrompt + successful text history\nQueryResultTable\nNo keys or database credentials')
box(slide, 4.1, 1.65, 3.4, 2.45, '2  ASP.NET Core API', 'POST /api/agent\nValidate prompt/history\nScoped tools + DbContext\nConfigured-origin CORS')
box(slide, 8, 1.65, 3.4, 2.45, '3  ChatClientAgent', 'Instructions + tool descriptions\nIChatClient invocation pipeline\nClarify missing details first\nSequential tool execution')
box(slide, 11.9, 1.65, 3.65, 2.45, '4  Azure OpenAI', 'AzureOpenAIClient\nConfigured chat deployment\nProduces text or tool calls\nReceives query STATUS only', ORANGE)
arrow(slide, 3.65, 2.6)
arrow(slide, 7.55, 2.6)
arrow(slide, 11.45, 2.6)
text(slide, 11.85, 4.13, 3.7, .45, 'Model reply / tool-call request', 13, align=PP_ALIGN.CENTER)
arrow(slide, 11.46, 3.42, direction='left')
arrow(slide, 9.5, 4.22, .35, .55, 'down')
box(slide, 8, 4.95, 3.4, 2.05, '5  AdventureworksTool', 'AIFunctionFactory: 22 tools\nCall matching query method\nCaptureResult saves real rows\nReturn Message to model', GREEN)
box(slide, 11.9, 4.95, 3.65, 2.05, '6  Query processor + SQL', 'Typed SqlParameter values\nEF Core relational reader\nAdventureWorks SELECTs\nResponseData: Message/Data', GREEN)
arrow(slide, 11.45, 5.55)
arrow(slide, 11.45, 6.35, direction='left', color='24805B')
text(slide, 8.0, 4.13, 3.4, .45, 'Invoke tool ↓   ↑ status to model', 13, align=PP_ALIGN.CENTER)
box(slide, 4.1, 4.95, 3.4, 2.05, '7  API response', 'message: agent text\nresults[]: tool, status, count\ndata: original database rows\nNo second SQL execution', GREEN)
box(slide, .45, 4.95, 3.15, 2.05, '8  Chat + data tables', 'Clarification or final reply\n25 returned rows per page\nNulls and numbers preserved\nClear chat resets history', GREEN)
arrow(slide, 7.55, 5.85, direction='left', color='24805B')
arrow(slide, 3.65, 5.85, direction='left', color='24805B')
text(slide, .5, 7.45, 15, .95, 'Key distinction: the agent sees tool status messages, while the browser receives full query rows directly.\nBrowser-supplied history is bounded text, not durable server memory. CORS is not authentication.', 19, True)
text(slide, .5, 8.55, 15, .28, 'Export this slide as PNG for the architecture placeholder in MAF-Article.html. All shapes and text are editable.', 12)

slide = prs.slides.add_slide(prs.slide_layouts[6])
text(slide, .45, .22, 15.1, .6, 'Follow-up questions before the database query', 29, True)
text(slide, .45, .95, 15.1, .6, 'Illustrative dialogue; exact wording is model-dependent. Earlier answers are sent with the next prompt.', 18)
steps = [
    ('User', 'Show revenue by region.', 'API sends this request to the agent.', BLUE),
    ('Agent', 'Which territory, country, group, or all territories?', 'Clarification only: no query result rows.', ORANGE),
    ('User', 'Northwest.', 'Previous user/assistant text supplies the report context.', BLUE),
    ('Agent', 'Which date range, or all available dates?', 'Region is now known; date choice is still missing.', ORANGE),
    ('User', 'All available dates.', 'The agent resolves region=Northwest; from/to=null.', BLUE),
    ('Tool + API', 'revenue_by_region → actual database rows', 'Status goes to the model; results[].data goes to the UI.', GREEN),
]
for index, (role, message, meaning, fill) in enumerate(steps):
    y = 1.8 + index * .91
    shape = slide.shapes.add_shape(MSO_SHAPE.ROUNDED_RECTANGLE, Inches(.55), Inches(y), Inches(14.9), Inches(.79))
    shape.fill.solid()
    shape.fill.fore_color.rgb = RGBColor.from_string(fill)
    shape.line.fill.background()
    text(slide, .7, y+.1, 1.55, .55, role, 17, True)
    text(slide, 2.3, y+.1, 6.25, .6, message, 17, True)
    text(slide, 8.7, y+.1, 6.5, .6, meaning, 15)
text(slide, .55, 7.55, 14.9, .9, 'Complete request? Run directly: “Revenue for Northwest using all available dates.”\nClear chat / reload resets context. Failed or canceled calls and query tables are not added to model history.', 19, True)
text(slide, .55, 8.55, 14.9, .28, 'History limits: 20 prior messages • 8,000 characters per message • 40,000 history characters. No shared server-side chat store.', 12)
prs.save(ROOT / 'MAF-Architecture.pptx')
print('Created MAF-Architecture.pptx with 2 editable slides.')
