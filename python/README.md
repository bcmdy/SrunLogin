# Srun 校园网认证工具

纯 Python 标准库实现的校园网登录脚本，支持 Dr.COM/SRun 认证系统。

## 功能

- 自动检测 IP 地址和 AC_ID（减少手动配置）
- 登录校园网
- 查询在线状态（已用流量、已用时长、余额）
- 查询账户到期时间
- 登出校园网
- 诊断输出（方便排查问题）

## 环境

- Python 3.7+
- 无需安装任何第三方库

## 快速开始

```bash
# 登录
python login.py login -u 账号 -p 密码 --url http://网关地址

# 查看在线状态
python login.py info -u 账号 --url http://网关地址

# 登出
python login.py logout -u 账号 -p 密码 --url http://网关地址
```

## 参数说明

| 参数 | 简写 | 说明 | 示例 |
|------|------|------|------|
| --url | - | 认证服务器地址 | http://10.0.0.1 |
| --username | -u | 用户名/账号 | 202600000000 |
| --password | -p | 密码 | ******** |
| --ip | - | 指定IP地址 (可选) | 自动检测 |
| --ac-id | - | AC ID (可选) | 自动检测 |
| --domain | - | 域 (可选) | @edu.cn |

## Windows 快捷方式

项目包含三个批处理文件，可以直接双击运行：

- [login.bat](login.bat) - 登录（修改文件中的账号密码后使用）
- [getinfo.bat](getinfo.bat) - 查询状态
- [loginout.bat](loginout.bat) - 登出

## 常见问题

### 1. 无法获取 IP

脚本会自动尝试多种方式获取本机 IP。如果失败，可手动指定：

```bash
python login.py login -u 账号 -p 密码 --url http://网关 --ip 10.189.150.176
```

### 2. ac_id 错误

自动检测失败时，手动指定：

```bash
python login.py login -u 账号 -p 密码 --url http://网关 --ac-id 143
```

### 3. "当前已在线"

说明账号已经登录成功，无需重复登录。

### 4. 认证信息加密错误

通常是 ac_id 不正确导致的，尝试更换 ac_id。

### 5. 在线设备数量超限

校园网限制了同时在线的设备数量，需要先登出其他设备。

## 技术细节

### 认证流程

1. 通过访问门户页面或 API 自动获取 IP 和 AC_ID
2. 获取 Token (challenge)
3. 计算加密密码: HMAC-MD5(token, password)
4. 加密用户信息: XXTEA + 自定义Base64
5. 计算签名: SHA1(token + 用户名 + 密码MD5 + ac_id + IP + ...)
6. 发送登录请求

### 自动检测逻辑

脚本按以下顺序尝试获取 IP 和 AC_ID：

1. 从门户网站 HTML 解析
2. 调用 `/v1/srun_portal_detect` 接口
3. 访问 `/srun_portal_pc` 页面
4. 尝试常用 AC_ID 候选列表
5. 通过 `/cgi-bin/rad_user_info` 获取 IP

## 目录结构

```
SrunLogin/
├── login.py      # 主程序
├── login.bat     # Windows 一键登录
├── getinfo.bat  # Windows 查询状态
├── loginout.bat # Windows 登出
└── README.md   # 说明文档
```

## 协议

MIT License