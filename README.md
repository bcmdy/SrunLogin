# Srun 校园网认证工具

纯 Python 标准库实现的校园网登录脚本，支持 Dr.COM/SRun 认证系统。

## 功能

- 登录校园网
- 查询在线状态
- 查询到期时间
- 登出校园网

## 环境

- Python 3.7+
- 无需安装任何第三方库

## 使用方法

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
| --url | - | 认证服务器地址 | http://1.1.1.1 |
| --username | -u | 用户名/账号 | 您的账号 |
| --password | -p | 密码 | ******** |
| --ip | - | 指定IP地址 (可选) | 自动获取 |
| --ac-id | - | AC ID (可选) | 自动获取 |
| --domain | - | 域 (可选) | @edu.cn |

## 示例

```bash
# 登录
python login.py login -u 您的账号 -p 您的密码 --url http://1.1.1.1

# 查看状态
python login.py info -u 您的账号 --url http://1.1.1.1

# 登出
python login.py logout -u 您的账号 -p 您的密码 --url http://1.1.1.1
```

## 常见问题

### 1. 无法获取IP

如果自动获取IP失败，可以手动指定：

```bash
python login.py login -u 账号 -p 密码 --url http://网关 --ip 您的IP
```

### 2. ac_id 错误

尝试手动指定：

```bash
python login.py login -u 账号 -p 密码 --url http://网关 --ac-id 143
```

### 3. "当前已在线"

说明账号已经登录成功，无需重复登录。


## 认证流程

1. 获取 Token (challenge)
2. 计算加密密码: HMAC-MD5(token, password)
3. 加密用户信息: XXTEA + 自定义Base64
4. 计算签名: SHA1(token + 用户名 + 密码MD5 + ac_id + IP + ...)
5. 发送登录请求

## 目录结构

```
.
├── login.py      # 主程序
└── README.md   # 说明文档
```

## 协议

MIT License