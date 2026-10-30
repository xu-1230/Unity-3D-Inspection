package model

import (
	"crypto/rand"
	"encoding/hex"
)

// NewID 生成 32 位十六进制的问题标识，取自 crypto/rand，进程内不重复。
func NewID() (string, error) {
	buf := make([]byte, 16)
	if _, err := rand.Read(buf); err != nil {
		return "", err
	}
	return hex.EncodeToString(buf), nil
}
