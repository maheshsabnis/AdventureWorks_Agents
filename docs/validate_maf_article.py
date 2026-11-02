"""Validate complete article listings, redaction, and the editable PowerPoint package."""
from pathlib import Path
from html.parser import HTMLParser
from pptx import Presentation
from zipfile import ZipFile
import json
import re
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
article = (ROOT / 'MAF-Article.html').read_text(encoding='utf-8')


class ArticleParser(HTMLParser):
    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.stack = []
        self.listings = {}
        self.current_source = None
        self.current_code = None
        self.json_listings = []
        self.headings = []
        self.heading = None

    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        assert tag not in ('html', 'head', 'body', 'style', 'script'), f'Unexpected document tag: {tag}'
        if tag not in ('br', 'hr', 'img', 'input', 'meta', 'link'):
            self.stack.append(tag)
        if tag == 'code':
            self.current_source = attrs.get('data-source')
            self.current_code = []
            self.is_json = attrs.get('class') == 'language-json'
        if tag == 'h2':
            self.heading = []

    def handle_endtag(self, tag):
        assert self.stack and self.stack.pop() == tag, f'Unbalanced HTML: {tag}'
        if tag == 'code':
            content = ''.join(self.current_code)
            if self.current_source:
                self.listings[self.current_source] = content
            if self.is_json:
                self.json_listings.append(json.loads(content))
            self.current_code = None
            self.current_source = None
        if tag == 'h2':
            self.headings.append(''.join(self.heading))
            self.heading = None

    def handle_data(self, data):
        if self.current_code is not None:
            self.current_code.append(data)
        if self.heading is not None:
            self.heading.append(data)


parser = ArticleParser()
parser.feed(article)
assert not parser.stack
assert article.lstrip().startswith('<article>')
assert len(parser.listings) == 17
for filename, content in parser.listings.items():
    assert content == (ROOT / filename).read_text(encoding='utf-8-sig'), f'Source listing differs: {filename}'
    assert not filename.endswith('.css')
    assert 'AdventureWorks2025Context.cs' not in filename
assert sum(heading.startswith('Step ') for heading in parser.headings) == 18
assert parser.headings[-1] == 'Conclusion'
assert 'architecture-image-placeholder' in article
assert 'MAF-Architecture.pptx' in article
assert article.count('private const string ') == 22
assert article.count('public async Task&lt;ResponseData&gt; Get') == 22
assert article.count('public async Task&lt;string&gt; ') == 22
for name in ['IChatClient','AzureOpenAIClient','ApiKeyCredential','AIAgent','ChatClientAgent','ChatClientAgentOptions','ChatOptions','AIFunctionFactory','DescriptionAttribute']:
    assert name in article

# Compare against local configuration without printing or publishing its values.
for path in (ROOT / 'AdventureWorksTool').glob('appsettings*.json'):
    settings = json.loads(path.read_text(encoding='utf-8-sig'))
    values = list(settings.get('ConnectionStrings', {}).values()) + list(settings.get('AzureOpenAI', {}).values())
    for value in values:
        if isinstance(value, str) and len(value) > 5 and not value.startswith('<'):
            assert value not in article, 'A server configuration value was found in the article.'
context = (ROOT / 'AdventureWorksTool/Models/AdventureWorks2025Context.cs').read_text(encoding='utf-8-sig')
for value in re.findall(r'UseSqlServer\("([^"\n]+)"', context):
    assert value not in article, 'An inline database connection was found in the article.'

pptx_path = ROOT / 'MAF-Architecture.pptx'
with ZipFile(pptx_path) as package:
    assert package.testzip() is None
    for name in package.namelist():
        if name.endswith(('.xml', '.rels')):
            ET.fromstring(package.read(name))
presentation = Presentation(pptx_path)
assert len(presentation.slides) == 2
for slide in presentation.slides:
    assert len(slide.shapes) > 10
    for shape in slide.shapes:
        assert shape.left >= 0 and shape.top >= 0
        assert shape.left + shape.width <= presentation.slide_width
        assert shape.top + shape.height <= presentation.slide_height
print('PASS: balanced Blogger fragment, 18 steps, 17 exact source listings, 22 queries/tools, valid JSON, no copied server secrets, and valid two-slide PowerPoint package.')
