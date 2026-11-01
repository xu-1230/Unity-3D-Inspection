// 一次性迁移工具：把旧的 data/issues.json 导入 SQLite。
//
//	go run ./cmd/migrate -from data/issues.json -to data/issues.db
//
// 可重复执行，已导入的记录会按「标识 + 创建时间」跳过。
package main

import (
	"flag"
	"fmt"
	"log"

	"unity-3d-inspection/backend/internal/migrate"
)

func main() {
	from := flag.String("from", "data/issues.json", "旧 JSON 数据文件路径")
	to := flag.String("to", "data/issues.db", "目标 SQLite 文件路径")
	flag.Parse()

	report, err := migrate.FromJSON(*from, *to)
	if err != nil {
		log.Fatalf("迁移失败：%v", err)
	}

	fmt.Printf("读取 %d 条，写入 %d 条，跳过 %d 条 -> %s\n",
		report.Read, report.Inserted, report.Skipped, *to)
}
