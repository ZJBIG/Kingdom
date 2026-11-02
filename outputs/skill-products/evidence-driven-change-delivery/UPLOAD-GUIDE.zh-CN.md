# 首版交付与 Agensi 上架步骤

当前状态：本地首版候选包；用户选择先交付，稍后上传。尚未创建商品草稿、设置实际价格或提交审核。

## 文件怎么用

- `evidence-driven-change-delivery/`：可独立保存的技能源文件，只有 SKILL.md 和两个 references 文件。
- `evidence-driven-change-delivery-v0.1.1.zip`：当前准备提交的平台包。不要上传整个产品工作目录。
- `LISTING.md`：可粘贴的英文商品文案，包含标题、简介、用途、前提、兼容性边界和建议定价。
- `VALIDATION.md`：本轮结构与行为验证记录。请根据该文件中的实际证据理解商品承诺。
- `SOURCE-NOTES.md`：来源和交付边界，不放进出售的 ZIP。

## 稍后上传

1. 打开 https://www.agensi.io/dashboard/submit 并登录。若需要注册，由账号本人核对条款并完成注册；不要把密码发到对话中。
2. 上传 `evidence-driven-change-delivery-v0.1.1.zip`。包内根目录直接包含 SKILL.md，并保留 references 子目录。本轮尚未进入登录后的表单；实际上传校验仍需在平台完成。
3. 按 LISTING.md 填写名称、简介、说明、示例、前提和兼容范围。分类优先 Code Quality & Review；如允许，再选择 Testing & Debugging。
4. 首发建议为 US$9 单次购买，不设置订阅和未承诺的更新周期。这是试销建议，不是已经验证的市场定价。
5. 付费发布前，按平台要求启用 Stripe Connect 或 Zoneless 收款并完成所需身份核验。先核对自己所在地的开户、核验、提现支持；本轮没有替你开通金融账户或提交个人资料。
6. 核对预览、价格与承诺，再提交审核。平台公开说明含安全扫描和审核，通常 24–48 小时；实际结果以后台为准。

## 发布前的真实边界

这套技能提供跨技术栈的工作方法，不是自动代码分析器。当前测试使用隔离的合成样例，不能宣传为已验证适用于所有开发者、所有语言或全部客户端。真实项目与平台连接器上的试用仍有价值。

Agensi 当前条款规定买家通过其 agent 连接加载技能，不提供买家下载；你保留这份独立源文件。不要在平台商品文案中承诺可下载 ZIP，或添加与平台冲突的退款条款、买家许可及站外付费导流。

参考（2026-10-24 查阅）：

- https://www.agensi.io/sell
- https://www.agensi.io/terms
- https://www.agensi.io/learn/how-developers-make-money-selling-ai-skills

上架步骤以公开说明整理，登录后字段、最终许可和收款条件以实际页面为准。
